// Assets/Scripts/Data/UserJsonModels.cs
// Shared JSON models for entries + sessions + samples.
// Keep these in sync across all writers. British English comments.

using System;
using System.Collections.Generic;

namespace ExperimentData
{
    [Serializable]
    public class Sample
    {
        public string tIso;  // ISO-8601 (UTC)
        public string type;
        public int    score;
        public double z, raw, alpha, beta, theta, gamma;
        public string label;
        public int    count, target;
        public double elapsed;
        public double mu_raw, sigma_raw, mu_gamma, sigma_gamma, mu_total, sigma_total;
    }

    [Serializable]
    public class UserSession
    {
        public string username;
        public string scene;
        public string sessionId;
        public string startedAtIso;
        public List<Sample> samples = new List<Sample>();
    }

    [Serializable]
    public class UserDataEntry
    {
        public string username;
        public string createdAtIso;
        public string updatedAtIso;
        public int    version = 1;
    }

    // Unified file: entries + sessions in the same JSON (UserData.json)
    [Serializable]
    public class ExtendedUserDataContainer
    {
        public List<UserDataEntry> entries  = new List<UserDataEntry>();
        public List<UserSession>   sessions = new List<UserSession>();
    }
}
