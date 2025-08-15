/*
 * OSCCommandParser.cs
 *
 * Parses OSC control messages and routes them to RTMLCore.
 * Attach alongside OSCReceiver and RTMLCore.
 * Binds addresses like "/rtml/control/record" to toggle modes at runtime.
 */


using UnityEngine;

namespace RTMLToolKit
{
    /// <summary>
    /// Parses OSC control messages and routes them into RTMLCore.
    /// Attach this alongside OSCReceiver and RTMLCore on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(OSCReceiver))]
    [RequireComponent(typeof(RTMLCore))]
    public class OSCCommandParser : MonoBehaviour
    {
        [Tooltip("Reference to the OSCReceiver component")]
        public OSCReceiver receiver;

        [Tooltip("Reference to the RTMLCore component")]
        public RTMLCore core;

        void Awake()
        {
            // Auto-assign if not set in Inspector
            if (receiver == null) receiver = GetComponent<OSCReceiver>();
            if (core     == null) core     = GetComponent<RTMLCore>();

            // Bind control addresses
            receiver.Bind("/rtml/control/record", HandleRecord);
            receiver.Bind("/rtml/control/train",  HandleTrain);
            receiver.Bind("/rtml/control/run",    HandleRun);
        }

        private void HandleRecord(string address, float value)
        {
            core.enableRecord = (value > 0.5f);
            Logger.Log($"[OSCCommandParser] Record mode set to {core.enableRecord}");
        }

        private void HandleTrain(string address, float value)
        {
            if (value > 0.5f)
            {
                // RTMLCore already listens for this internally; 
                // if you’ve exposed a public TrainModel() you could call it here:
                // core.TrainModel();
                Logger.Log("[OSCCommandParser] Train command received");
            }
        }

        private void HandleRun(string address, float value)
        {
            core.enableRun = (value > 0.5f);
            Logger.Log($"[OSCCommandParser] Run mode set to {core.enableRun}");
        }
    }
}
