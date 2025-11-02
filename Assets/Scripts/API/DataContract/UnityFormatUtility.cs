using System;
using System.Collections.Generic;

[Serializable]
public class StudyForUnity {
    public int id;
    public string title;
    public string code;
    public string studyDate;
    public bool isVr;
    public string converationURL;

    public PatientInfo patient;
    public List<VrdfAsset> vrdfAssets;
}

[Serializable]
public class PatientInfo {
    public int id;
    public string firstName;
    public string lastName;
    public string dateOfBirth;
    public string gender;
}

[Serializable]
public class VrdfAsset {
    public string filename;
    public string modality;
    public string downloadUrl;
}
