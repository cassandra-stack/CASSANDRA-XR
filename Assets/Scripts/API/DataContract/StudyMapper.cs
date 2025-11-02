using System;
using System.Collections.Generic;

public static class StudyMapper
{
    public static StudyForUnity Map(StudyRaw raw)
    {
        var study = new StudyForUnity
        {
            id = raw.id,
            title = raw.title,
            code = raw.code,
            studyDate = raw.study_date,
            isVr = raw.is_vr,
            converationURL = raw.conversation_url,
            patient = new PatientInfo
            {
                id = raw.patient.id,
                firstName = raw.patient.first_name,
                lastName = raw.patient.last_name,
                dateOfBirth = raw.patient.date_of_birth,
                gender = raw.patient.gender
            },
            vrdfAssets = new List<VrdfAsset>()
        };

        foreach (var asset in raw.assets)
        {
            bool looksLikeVrdf =
                asset.filename != null && asset.filename.EndsWith(".vrdf", StringComparison.OrdinalIgnoreCase) &&
                asset.asset_type != null && asset.asset_type.EndsWith("_vrdf", StringComparison.OrdinalIgnoreCase);

            if (!looksLikeVrdf)
                continue;

            string modality = ExtractModality(asset);

            study.vrdfAssets.Add(new VrdfAsset
            {
                filename = asset.filename,
                modality = modality,
                downloadUrl = asset.download_url
            });
        }

        return study;
    }

    private static string ExtractModality(AssetRaw asset)
    {
        if (!string.IsNullOrEmpty(asset.asset_type))
        {
            var idx = asset.asset_type.IndexOf("_vrdf", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                return asset.asset_type.Substring(0, idx);
        }

        if (!string.IsNullOrEmpty(asset.filename))
        {
            var parts = asset.filename.Split('-');
            var lastPart = parts[parts.Length - 1];
            var underscoreIdx = lastPart.IndexOf("_");
            if (underscoreIdx > 0)
            {
                return lastPart.Substring(0, underscoreIdx);
            }
        }

        return "unknown";
    }
}
