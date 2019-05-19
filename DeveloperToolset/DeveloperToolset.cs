using MSCLoader;
using UnityEngine;

namespace DeveloperToolset
{
    public class DeveloperToolset : Mod
    {
        public override string ID => "DeveloperToolkit"; //Your mod ID (unique)
        public override string Name => "Developer Toolkit"; //You mod name
        public override string Author => "zamp, piotrulos"; //Your Username
        public override string Version => "1.1"; //Version

        // Set this to true if you will be load custom assets from Assets folder.
        // This will create subfolder in Assets folder for your mod.
        public override bool UseAssetsFolder => false;
        public override bool LoadInMenu => true;

        public Keybind showGui = new Keybind("showgui", "Show GUI", KeyCode.Z, KeyCode.LeftControl);
        public Keybind copy = new Keybind("copy", "Copy", KeyCode.C, KeyCode.LeftControl);
        public Keybind paste = new Keybind("paste", "Paste", KeyCode.V, KeyCode.LeftControl);
        public Keybind raycastTweakable = new Keybind("raycastTweakable", "Raycast tweakable", KeyCode.G, KeyCode.LeftControl);
        private Transform m_copy;
        private string m_copiedStr;
        private float m_copiedStrTime;

        public override void OnMenuLoad()
        {
            Keybind.Add(this, showGui);
            Keybind.Add(this, raycastTweakable);
            Inspector.Search("");
        }

        public override void OnLoad()
        {
            Inspector.Search("");
        }

        public override void OnNewGame()
        {
            Inspector.Search("");
        }

        public override void Update()
        {
            if (showGui.IsDown())
                Inspector.showGUI = !Inspector.showGUI;

            if (Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Physics.Raycast(ray, out RaycastHit hit);
                if (hit.collider)
                {
                    if (raycastTweakable.IsPressed())
                    {
                        Inspector.SetInspect(hit.collider.transform);
                    }
                    if (copy.IsDown())
                    {
                        m_copy = hit.collider.transform;
                        m_copiedStr = m_copy.name + " copied!";
                        m_copiedStrTime = 2f;
                    }
                    if (paste.IsDown())
                    {
                        Vector3 pos = Camera.main.transform.position + Camera.main.transform.forward * 2f;
                        GameObject clone = GameObject.Instantiate(m_copy.gameObject);
                        clone.name = m_copy.gameObject.name;
                        clone.transform.position = pos;
                    }
                }
            }
        }
        private Transform FindClosesBoneTo(Transform transform, Vector3 point)
        {
            // go through children
            float d = Vector3.Distance(transform.position, point);
            foreach (Transform t in transform)
            {
                // is closer to point than parent
                if (Vector3.Distance(t.position, point) < d)
                {
                    // return whatever child returns
                    return FindClosesBoneTo(t, point);
                }
            }
            return transform;
        }

        public override void OnGUI()
        {
            if (m_copiedStrTime > 0)
            {
                m_copiedStrTime -= Time.deltaTime;
                GUI.Label(new Rect(Screen.width / 2, Screen.height / 2, 200, 40), m_copiedStr);
            }
            Inspector.OnGUI();
        }
    }
}
