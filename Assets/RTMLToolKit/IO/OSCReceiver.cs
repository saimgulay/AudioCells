/*
 * OSCReceiver.cs
 *
 * Minimal OSC listener using UDP.
 * Attach to a GameObject and call Initialise(port) at Awake().
 * Bind string addresses to Action<float[]> or Action<string, float> handlers to process incoming messages.
 */

using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;  


namespace RTMLToolKit
{
    /// <summary>
    /// Minimal OSC receiver for RTML Tool Kit.
    /// Listens on a UDP port and parses incoming OSC messages with float arguments.
    /// Supports binding handlers for data and control messages.
    /// </summary>
    public class OSCReceiver : MonoBehaviour
    {
        private UdpClient udpClient;
        private Thread receiveThread;
        private bool isRunning;

        private ConcurrentDictionary<string, Action<float[]>> dataHandlers = new ConcurrentDictionary<string, Action<float[]>>();
        private ConcurrentDictionary<string, Action<string, float>> commandHandlers = new ConcurrentDictionary<string, Action<string, float>>();

        /// <summary>
        /// Initialise the receiver on the specified port.
        /// </summary>
        public void Initialize(int port)
        {
            udpClient = new UdpClient(port);
            isRunning = true;
            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();
            Logger.Log($"[OSCReceiver] Listening on UDP port {port}");
        }

        /// <summary>
        /// Bind a handler for OSC messages that carry float array data.
        /// </summary>
        public void Bind(string address, Action<float[]> handler)
        {
            dataHandlers[address] = handler;
        }

        /// <summary>
        /// Bind a handler for OSC control messages with a single float argument.
        /// </summary>
        public void Bind(string address, Action<string, float> handler)
        {
            commandHandlers[address] = handler;
        }

        /// <summary>
        /// Stops the receiver and cleans up resources.
        /// </summary>
        public void Close()
        {
            isRunning = false;
            udpClient?.Close();
        }

        private void ReceiveLoop()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            while (isRunning)
            {
                try
                {
                    byte[] data = udpClient.Receive(ref remoteEP);
                    ParseOscMessage(data);
                }
                catch (Exception ex)
                {
                    if (isRunning)
                        Logger.LogWarning($"[OSCReceiver] Receive error: {ex.Message}");
                }
            }
        }

        private void ParseOscMessage(byte[] data)
        {
            int index = 0;
            string address = ReadString(data, ref index);
            string typeTags = ReadString(data, ref index);

            var args = new List<float>();
            if (typeTags.Length > 1 && typeTags[0] == ',')
            {
                for (int t = 1; t < typeTags.Length; t++)
                {
                    if (typeTags[t] == 'f')
                    {
                        if (index + 4 <= data.Length)
                        {
                            byte[] floatBytes = new byte[4];
                            Array.Copy(data, index, floatBytes, 0, 4);
                            if (BitConverter.IsLittleEndian)
                                Array.Reverse(floatBytes);
                            float value = BitConverter.ToSingle(floatBytes, 0);
                            args.Add(value);
                            index += 4;
                        }
                    }
                    else
                    {
                        // Unsupported type, skip 4 bytes
                        index += 4;
                    }
                }
            }

            // Invoke data handler if present
            if (dataHandlers.TryGetValue(address, out var dataHandler))
            {
                dataHandler.Invoke(args.ToArray());
            }

            // Invoke command handler if exactly one float
            if (args.Count == 1 && commandHandlers.TryGetValue(address, out var cmdHandler))
            {
                cmdHandler.Invoke(address, args[0]);
            }
        }

        private string ReadString(byte[] data, ref int index)
        {
            int start = index;
            while (index < data.Length && data[index] != 0)
                index++;

            string result = Encoding.UTF8.GetString(data, start, index - start);

            // Advance index to next 4-byte boundary
            int pad = 4 - ((index + 1) % 4);
            if (pad < 4)
                index += pad;
            index++; // skip null terminator

            return result;
        }
    }
}
