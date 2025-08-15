# brainbit_streamer.py
# Native BLE → BrainFlow Streaming Board (multicast)
# Baseline (60 s), denoise, artefact veto (configurable), z-score, 0–100 concentration.
# Fix: no repeated windows (own buffer using get_board_data()).
# Sends metrics as UDP JSON; listens for UDP {"cmd":"shutdown"} for graceful stop.
# British English comments.

import time
import math
import argparse
import json
import socket
import threading
import numpy as np
from collections import deque
from brainflow import (
    BoardShim, BrainFlowInputParams, BoardIds, BrainFlowError, DataFilter,
    DetrendOperations, FilterTypes, WaveletTypes, NoiseTypes
)

# ---- Streaming Board (raw EEG forwarding) ----
MULTICAST_IP_DEFAULT = "225.1.1.1"
PORT_DEFAULT = 6677

# ---- Metric UDP (JSON to Unity) ----
METRIC_HOST_DEFAULT = "127.0.0.1"
METRIC_PORT_DEFAULT = 7788

# ---- Control UDP (Unity → Python) ----
CONTROL_HOST_DEFAULT = "127.0.0.1"
CONTROL_PORT_DEFAULT = 7790   # Unity sends {"cmd":"shutdown"} here

WINDOW_SEC = 5
BASELINE_SEC = 30
LOGISTIC_GAIN = 1.2
HIGH_Z = 1.0
LOW_Z  = -1.0
EPS = 1e-12

def sigmoid(x: float) -> float:
    return 1.0 / (1.0 + math.exp(-x))

def finite_all(arr) -> bool:
    return np.all(np.isfinite(arr))

def clean_window(data_2d: np.ndarray, eeg_channels, sr: int):
    """Light denoising per channel: detrend → 50 Hz notch → 1–45 Hz bandpass → wavelet denoise."""
    x = data_2d.copy()
    for ch in eeg_channels:
        sig = np.array(x[ch], dtype=np.float64, copy=True)
        DataFilter.detrend(sig, DetrendOperations.CONSTANT.value)
        DataFilter.remove_environmental_noise(sig, sr, NoiseTypes.FIFTY.value)
        DataFilter.perform_bandpass(sig, sr, 1.0, 45.0, 4, FilterTypes.BUTTERWORTH.value, 0.0)
        den = DataFilter.perform_wavelet_denoising(sig, WaveletTypes.BIOR3_9.value, 3)
        sig = np.asarray(den, dtype=np.float64)
        if not finite_all(sig):
            # fallback if wavelet behaves oddly
            sig = np.array(x[ch], dtype=np.float64, copy=True)
            DataFilter.detrend(sig, DetrendOperations.CONSTANT.value)
            DataFilter.remove_environmental_noise(sig, sr, NoiseTypes.FIFTY.value)
            DataFilter.perform_bandpass(sig, sr, 1.0, 45.0, 4, FilterTypes.BUTTERWORTH.value, 0.0)
        x[ch] = sig
    return x

def get_bands(data_2d: np.ndarray, eeg_channels, sr: int, already_clean: bool):
    """Return (delta, theta, alpha, beta, gamma) averaged over EEG channels."""
    bands, _ = DataFilter.get_avg_band_powers(data_2d, eeg_channels, sr, (not already_clean))
    return bands

def mean_std(xs):
    m = float(np.mean(xs))
    s = float(np.std(xs, ddof=1)) if len(xs) > 1 else EPS
    return m, max(s, EPS)

def robust_mean_std(xs):
    xs = np.asarray(xs, dtype=np.float64)
    med = float(np.median(xs))
    mad = float(np.median(np.abs(xs - med)))
    sigma = 1.4826 * mad
    return med, max(sigma, EPS)

class ControlServer(threading.Thread):
    """Listen for JSON commands: {'cmd':'shutdown'} to set stop_event."""
    def __init__(self, host: str, port: int, stop_event: threading.Event):
        super().__init__(daemon=True)
        self.host = host
        self.port = port
        self.stop_event = stop_event
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.bind((host, port))
        self.sock.settimeout(0.5)

    def run(self):
        print(f"[Control] Listening on {self.host}:{self.port}")
        while not self.stop_event.is_set():
            try:
                data, _ = self.sock.recvfrom(4096)
                msg = json.loads(data.decode("utf-8"))
                if isinstance(msg, dict) and msg.get("cmd") == "shutdown":
                    print("[Control] Shutdown command received.")
                    self.stop_event.set()
            except socket.timeout:
                continue
            except Exception as e:
                print(f"[Control] Error: {e}")
        try:
            self.sock.close()
        except Exception:
            pass

def sleep_until(seconds: float, stop_event: threading.Event, step: float = 0.2) -> bool:
    """Sleep up to 'seconds' but wake early if stop_event is set. Returns True if stopped early."""
    t = 0.0
    while t < seconds:
        if stop_event.is_set():
            return True
        dt = min(step, seconds - t)
        time.sleep(dt)
        t += dt
    return stop_event.is_set()

def append_buffer(buf: np.ndarray | None, new: np.ndarray, max_keep: int) -> np.ndarray:
    """Append new samples along axis=1, keep at most 'max_keep' latest columns."""
    if new.size == 0:
        return buf if buf is not None else np.empty((0, 0))
    if buf is None or buf.size == 0:
        out = new.copy()
    else:
        out = np.concatenate([buf, new], axis=1)
    if out.shape[1] > max_keep:
        out = out[:, -max_keep:]
    return out

def consume_oldest(buf: np.ndarray, n: int) -> tuple[np.ndarray, np.ndarray]:
    """Return (window=oldest n cols, remainder=rest). Assumes buf.shape[1] >= n."""
    win = buf[:, :n]
    rem = buf[:, n:]
    return win, rem

def main():
    parser = argparse.ArgumentParser(description="BrainBit native BLE → Streaming Board with baseline, denoise, configurable veto, z-score, 0–100 focus (+ control UDP).")
    parser.add_argument("--serial", type=str, default="", help="Optional BrainBit serial number (printed on the device).")
    parser.add_argument("--timeout", type=int, default=45, help="Discovery timeout in SECONDS (default: 45).")
    parser.add_argument("--ip", type=str, default=MULTICAST_IP_DEFAULT, help="Multicast IP for Streaming Board.")
    parser.add_argument("--port", type=int, default=PORT_DEFAULT, help="UDP port for Streaming Board.")
    parser.add_argument("--no-clean", action="store_true", help="Disable manual denoising pipeline.")
    parser.add_argument("--veto-mode", type=str, default="lenient", choices=["off", "lenient", "strict"], help="Artefact veto aggressiveness.")
    parser.add_argument("--veto-z", type=float, default=3.0, help="Base Z-threshold (gamma/total power).")
    parser.add_argument("--max-consec-veto", type=int, default=2, help="Limit consecutive veto windows.")
    parser.add_argument("--metric-host", type=str, default=METRIC_HOST_DEFAULT, help="UDP host for JSON metrics (Unity).")
    parser.add_argument("--metric-port", type=int, default=METRIC_PORT_DEFAULT, help="UDP port for JSON metrics (Unity).")
    parser.add_argument("--control-host", type=str, default=CONTROL_HOST_DEFAULT, help="UDP host to listen for control commands.")
    parser.add_argument("--control-port", type=int, default=CONTROL_PORT_DEFAULT, help="UDP port to listen for control commands.")
    args = parser.parse_args()

    BoardShim.enable_dev_board_logger()

    ip = BrainFlowInputParams()
    ip.timeout = int(args.timeout)   # seconds
    if args.serial.strip():
        ip.serial_number = args.serial.strip()

    board_id = BoardIds.BRAINBIT_BOARD.value
    stream_url = f"streaming_board://{args.ip}:{args.port}"
    use_clean = (not args.no_clean)
    veto_mode = args.veto_mode
    veto_thr = float(args.veto_z)
    max_consec_veto = max(0, int(args.max_consec_veto))

    # UDP socket for metrics
    udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp_addr = (args.metric_host, int(args.metric_port))

    stop_event = threading.Event()
    control = ControlServer(args.control_host, int(args.control_port), stop_event)
    control.start()

    shim = None
    try:
        # Prepare (with retries)
        def connect_with_retries(retries=3, pause=3):
            last = None
            for i in range(1, retries+1):
                try:
                    print(f"[Connect] Preparing session (attempt {i}/{retries})…")
                    s = BoardShim(board_id, ip)
                    s.prepare_session()
                    return s
                except Exception as e:
                    last = e
                    print(f"[Connect] Error: {e} — retrying in {pause}s")
                    if sleep_until(pause, stop_event): break
            raise RuntimeError(f"Unable to prepare session: {last}")

        shim = connect_with_retries()

        # Start stream & add streamer
        print("[Streamer] Starting acquisition…")
        shim.start_stream(45000)
        shim.add_streamer(stream_url)
        print(f"[Streamer] Forwarding to {stream_url}")
        print(f"[Metrics] UDP JSON → {udp_addr[0]}:{udp_addr[1]}")

        sr = BoardShim.get_sampling_rate(board_id)
        eeg = BoardShim.get_eeg_channels(board_id)
        needed = sr * WINDOW_SEC
        keep_cols = needed * 4  # cap internal buffer to avoid growth

        # ---------- Personal baseline ----------
        print(f"[Baseline] Collecting {BASELINE_SEC}s neutral baseline… (clean={use_clean}, veto={veto_mode}, z={veto_thr})")
        baseline_windows = max(1, BASELINE_SEC // WINDOW_SEC)
        baseline_timebox = baseline_windows * 8  # allow more chances
        raws, gammas, totals = [], [], []
        taken = 0
        t0 = time.time()

        buf = None  # internal rolling buffer (2D: channels x samples)
        while len(raws) < baseline_windows and taken < baseline_timebox and not stop_event.is_set():
            # append whatever arrived since last check, then try to consume exactly one non-overlapping window
            new = shim.get_board_data()  # returns all new and CLEARS internal ring
            if new.size > 0:
                buf = append_buffer(buf, new, keep_cols)
            if buf is None or buf.size == 0 or buf.shape[1] < needed:
                # not enough fresh data yet
                if sleep_until(0.2, stop_event): break
                continue

            # consume oldest 'needed' samples → guarantees no window repetition
            window, buf = consume_oldest(buf, needed)
            taken += 1

            data = window
            if use_clean:
                data_c = clean_window(data, eeg, sr)
                if not finite_all(data_c):
                    print("[Baseline] Non-finite after cleaning — discarded.")
                    continue
                bands = get_bands(data_c, eeg, sr, already_clean=True)
            else:
                if not finite_all(data):
                    print("[Baseline] Non-finite window — discarded.")
                    continue
                bands = get_bands(data, eeg, sr, already_clean=False)

            if not finite_all(bands) or any(b <= 0 for b in bands):
                print("[Baseline] Invalid bands (NaN/≤0) — discarded.")
                continue

            delta, theta, alpha, beta, gamma = bands
            raw = beta / (alpha + theta + EPS)
            total = sum(bands)
            if not math.isfinite(raw):
                print("[Baseline] raw non-finite — discarded.")
                continue

            raws.append(raw); gammas.append(gamma); totals.append(total)
            print(f"[Baseline] {len(raws)}/{baseline_windows}  raw={raw:.3f}")

            # notify progress
            udp.sendto(json.dumps({
                "type": "baseline_progress",
                "t": time.time(),
                "elapsed": time.time() - t0,
                "count": len(raws),
                "target": baseline_windows
            }).encode("utf-8"), udp_addr)

        if stop_event.is_set():
            print("[Main] Stopping during baseline upon request.")

        # Baseline stats
        if len(raws) >= max(3, baseline_windows // 2):
            mu_raw,  sigma_raw  = mean_std(raws)
            mu_gamma, sigma_gamma = mean_std(gammas)
            mu_total, sigma_total = mean_std(totals)
        elif len(raws) >= 3:
            print("[Baseline] Using robust statistics (median/MAD) due to scarce clean windows.")
            mu_raw,  sigma_raw  = robust_mean_std(raws)
            mu_gamma, sigma_gamma = robust_mean_std(gammas)
            mu_total, sigma_total = robust_mean_std(totals)
        else:
            raise RuntimeError("Baseline collection failed (insufficient clean windows).")

        print(f"[Baseline] raw:   μ={mu_raw:.4f}  σ={sigma_raw:.4f}  n={len(raws)} (taken {taken})")
        print(f"[Baseline] gamma: μ={mu_gamma:.4f}  σ={sigma_gamma:.4f}")
        print(f"[Baseline] total: μ={mu_total:.4f}  σ={sigma_total:.4f}")

        udp.sendto(json.dumps({
            "type": "baseline_ready",
            "t": time.time(),
            "mu_raw": mu_raw, "sigma_raw": sigma_raw,
            "mu_gamma": mu_gamma, "sigma_gamma": sigma_gamma,
            "mu_total": mu_total, "sigma_total": sigma_total
        }).encode("utf-8"), udp_addr)

        print("[Concentration] Ready. Reporting every 5 s…")

        # ---------- Ongoing measurement ----------
        consec_veto = 0
        last_emit_ts = 0.0

        while not stop_event.is_set():
            # accumulate new data continuously
            new = shim.get_board_data()
            if new.size > 0:
                buf = append_buffer(buf, new, keep_cols)

            if buf is None or buf.size == 0 or buf.shape[1] < needed:
                if sleep_until(0.05, stop_event): break
                continue

            # consume one non-overlapping window (oldest)
            window, buf = consume_oldest(buf, needed)
            data = window

            try:
                if use_clean:
                    data_c = clean_window(data, eeg, sr)
                    if not finite_all(data_c):
                        print("[Veto] Non-finite after cleaning — window discarded.")
                        continue
                    bands = get_bands(data_c, eeg, sr, already_clean=True)
                else:
                    if not finite_all(data):
                        print("[Veto] Non-finite window — discarded.")
                        continue
                    bands = get_bands(data, eeg, sr, already_clean=False)

                if not finite_all(bands) or any(b <= 0 for b in bands):
                    print("[Veto] Invalid bands (NaN/≤0) — window discarded.")
                    continue

                delta, theta, alpha, beta, gamma = bands
                total = sum(bands)

                # ── Artefact veto (configurable) ─────────────────────────────────────
                z_gamma = (gamma - mu_gamma) / max(sigma_gamma, EPS)
                z_total = (total - mu_total) / max(sigma_total, EPS)

                veto = False
                if veto_mode == "strict":
                    # very conservative
                    veto = (z_gamma > veto_thr) or (z_total > veto_thr)
                elif veto_mode == "lenient":
                    # tolerate spikes; require stronger evidence
                    veto = (z_gamma > (veto_thr + 1.0)) or ((z_total > (veto_thr + 1.0)) and (z_gamma > (veto_thr - 0.5)))
                else:  # "off"
                    veto = False

                # limit consecutive vetoes
                if veto and consec_veto >= max_consec_veto:
                    veto = False  # allow this one through
                if veto:
                    consec_veto += 1
                    print(f"[Veto] z_gamma={z_gamma:+.2f}  z_total={z_total:+.2f}  — discarded.")
                    udp.sendto(json.dumps({
                        "type": "veto",
                        "t": time.time(),
                        "z_gamma": z_gamma,
                        "z_total": z_total
                    }).encode("utf-8"), udp_addr)
                    continue
                else:
                    consec_veto = 0
                # ────────────────────────────────────────────────────────────────────

                # Concentration
                raw = beta / (alpha + theta + EPS)
                if not math.isfinite(raw):
                    print("[Veto] raw non-finite — window discarded.")
                    continue

                z = (raw - mu_raw) / max(sigma_raw, EPS)
                score01 = sigmoid(LOGISTIC_GAIN * z)
                score = int(round(100.0 * score01))
                score = max(0, min(100, score))

                label = "Neutral"
                if z >= HIGH_Z:   label = "High focus"
                elif z <= LOW_Z:  label = "Low focus"

                now = time.time()
                print(f"[Concentration] score={score:3d}  z={z:+.2f}  raw={raw:.3f}  "
                      f"|  α={alpha:.3f} β={beta:.3f} θ={theta:.3f} γ={gamma:.3f}  → {label}")
                last_emit_ts = now

                # Send JSON to Unity
                udp.sendto(json.dumps({
                    "type": "concentration",
                    "t": now,
                    "score": score,
                    "z": z,
                    "raw": raw,
                    "alpha": alpha, "beta": beta, "theta": theta, "gamma": gamma,
                    "label": label
                }).encode("utf-8"), udp_addr)

                # Also mark inside BrainFlow stream
                try:
                    shim.insert_marker(float(score))
                except Exception:
                    pass

                # Pace roughly to WINDOW_SEC if buffer ran far ahead
                sleep_until(max(0.0, WINDOW_SEC - 0.05), stop_event)

            except BrainFlowError as e:
                print(f"[Concentration] BrainFlowError whilst reading: {e}")

    except Exception as ex:
        print(f"[Main] Error: {ex}")
        print("Hints: Ensure the terminal app has Bluetooth permission (Privacy → Bluetooth), "
              "toggle the headset off/on, and make sure no other app holds the device.")
    finally:
        try:
            if shim is not None:
                try: shim.stop_stream()
                except Exception: pass
                try: shim.release_session()
                except Exception: pass
        finally:
            print("[Streamer] Bye.")

if __name__ == "__main__":
    main()
