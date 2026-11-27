using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Small "PING 42 ms" readout in the bottom-right corner during networked sessions.
/// Clients show their round-trip time to the host; the host shows the average RTT of its clients.
/// Green &lt; 80 ms, yellow &lt; 160 ms, red above.
/// </summary>
public class PingDisplay : MonoBehaviour
{
    TMP_Text _text;
    float _next;

    public static PingDisplay Create(Transform canvas, TMP_FontAsset font)
    {
        var go = new GameObject("PingDisplay", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-18f, 14f);
        rt.sizeDelta = new Vector2(260f, 30f);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = 16f;
        t.alignment = TextAlignmentOptions.BottomRight;
        t.raycastTarget = false;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(0, 0, 0, 200);
        var p = go.AddComponent<PingDisplay>();
        p._text = t;
        return p;
    }

    public void SetVisible(bool on)
    {
        if (_text != null && _text.enabled != on) _text.enabled = on;
    }

    void Update()
    {
        if (_text == null || !_text.enabled || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.5f;

        var nm = NetworkManager.Singleton;
        var transport = nm != null ? nm.NetworkConfig.NetworkTransport as UnityTransport : null;
        if (nm == null || transport == null || !nm.IsListening) { _text.text = ""; return; }

        ulong rtt;
        string suffix = "";
        if (nm.IsServer)
        {
            ulong sum = 0; int n = 0;
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (id == nm.LocalClientId) continue;
                sum += transport.GetCurrentRtt(id); n++;
            }
            if (n == 0) { _text.text = "<color=#9FB4CC>PING -- (HOST)</color>"; return; }
            rtt = sum / (ulong)n;
            suffix = " (HOST AVG)";
        }
        else
        {
            rtt = transport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        string col = rtt < 80 ? "#66FF88" : rtt < 160 ? "#FFD126" : "#FF5A4A";
        _text.text = $"<color={col}>PING {rtt} ms</color><color=#9FB4CC>{suffix}</color>";
    }
}
