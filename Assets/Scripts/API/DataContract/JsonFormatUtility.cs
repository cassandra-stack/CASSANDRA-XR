using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public class RootResponse
{
    [JsonProperty("success")]
    public bool success;

    [JsonProperty("data")]
    public List<StudyRaw> data;

    [JsonProperty("count")]
    public int count;

    [JsonProperty("picovoice_key")]
    public string picovoice_key;
}

[Serializable]
public class StudyRaw {
    [JsonProperty("id")]
    public int id;

    [JsonProperty("title")]
    public string title;

    [JsonProperty("code")]
    public string code;

    [JsonProperty("patient_id")]
    public int patient_id;

    [JsonProperty("status")]
    public string status;

    [JsonProperty("study_date")]
    public string study_date;

    [JsonProperty("is_vr")]
    public bool is_vr;

    [JsonProperty("conversation_url")]
    public string conversation_url;

    [JsonProperty("assets")]
    public List<AssetRaw> assets;

    [JsonProperty("patient")]
    public PatientRaw patient;
}

[Serializable]
public class AssetRaw {
    [JsonProperty("id")]
    public int id;

    [JsonProperty("filename")]
    public string filename;

    [JsonProperty("asset_type")]
    public string asset_type;

    [JsonProperty("download_url")]
    public string download_url;
}

[Serializable]
public class PatientRaw {
    [JsonProperty("id")]
    public int id;

    [JsonProperty("first_name")]
    public string first_name;

    [JsonProperty("last_name")]
    public string last_name;

    [JsonProperty("date_of_birth")]
    public string date_of_birth;

    [JsonProperty("gender")]
    public string gender;
}
