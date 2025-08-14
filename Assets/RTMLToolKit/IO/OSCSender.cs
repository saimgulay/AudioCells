/*
 * OSCSender.cs
 *
 * Minimal OSC sender using UDP.
 * Attach to a GameObject and call Initialise(remoteIP, remotePort).
 * Use Send(address, float[] values) to transmit OSC messages with float arguments.
 */


using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

namespace RTMLToolKit
{
    /// <summary>
    /// Minimal OSC sender for RTML Tool Kit.
    /// Constructs OSC messages with float arguments and sends via UDP.
    /// </summary>
    public class OSCSender : MonoBehaviour
    {
        private UdpClient udpClient;
        private IPEndPoint remoteEndPoint;

        /// <summary>
        /// Initialise the sender with a remote IP and port.
        /// </summary>
        public void Initialize(string ip, int port)
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            udpClient = new UdpClient();
            Logger.Log($"[OSCSender] Initialised sender to {ip}:{port}");
        }

        /// <summary>
        /// Send a float array as an OSC message to the given address.
        /// </summary>
        public void Send(string address, float[] values)
        {
            try
            {
                byte[] packet = BuildOscPacket(address, values);
                udpClient.Send(packet, packet.Length, remoteEndPoint);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[OSCSender] Send error: {ex.Message}");
            }
        }

        /// <summary>
        /// Close the UDP client.
        /// </summary>
        public void Close()
        {
            udpClient?.Close();
        }

        /// <summary>
        /// Build a raw OSC packet from an address and float arguments.
        /// </summary>
        private byte[] BuildOscPacket(string address, float[] values)
        {
            var data = new List<byte>();
            WritePaddedString(data, address);

            // Type tag string begins with comma
            string typeTags = "," + new string('f', values.Length);
            WritePaddedString(data, typeTags);

            // Append each float in big-endian
            foreach (var v in values)
            {
                byte[] bytes = BitConverter.GetBytes(v);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);
                data.AddRange(bytes);
            }

            return data.ToArray();
        }

        /// <summary>
        /// Write a null-terminated, 4-byte aligned string to the byte list.
        /// </summary>
        private void WritePaddedString(List<byte> data, string str)
        {
            byte[] s = Encoding.UTF8.GetBytes(str);
            data.AddRange(s);
            data.Add(0);

            // Pad to the next 4-byte boundary
            int pad = 4 - (data.Count % 4);
            if (pad < 4)
                data.AddRange(new byte[pad]);
        }
    }
}
