using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Conteneur pour les clés/endpoints récupérés de l'API.
/// </summary>
public struct ApiConfig
{
    public string PicovoiceKey;
    public string SttUrl;
    public string TtsUrl;
}

public class StudyService : MonoBehaviour
{
    [SerializeField]
    private string endpointUrl =
        "https://holonauts.fr/active";

    public IEnumerator FetchStudies(System.Action<List<StudyForUnity>, string, ApiConfig> onDone, string defaultCode)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(endpointUrl))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.certificateHandler = new CertsHandler();
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error fetching studies: " + req.error);
                onDone?.Invoke(null, defaultCode, new ApiConfig());
                yield break;
            }

            var root = JsonConvert.DeserializeObject<RootResponse>(req.downloadHandler.text);
            List<StudyForUnity> studies = new List<StudyForUnity>();
            foreach (var studyRaw in root.data)
            {
                studies.Add(StudyMapper.Map(studyRaw));
            }

            var config = new ApiConfig
            {
                PicovoiceKey = root.picovoice_key,
                SttUrl = root.stt_key,
                TtsUrl = root.tts_key
            };
            onDone?.Invoke(studies, defaultCode, config);
        }
    }

}
