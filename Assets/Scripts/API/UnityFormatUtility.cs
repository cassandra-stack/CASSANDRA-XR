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
    public string filename;     // ex: "BraTS-GLI-00014-000-t1c_lw.vrdf"
    public string modality;     // ex: "t1c"
    public string downloadUrl;  // ex: "https://holonauts.fr/private-storage/..."
}
