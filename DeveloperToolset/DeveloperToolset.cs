using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace DeveloperToolset
{
    public class DeveloperToolset : Mod
    { 
        /*
        Changelog 2019-10-03
            Added PhysicMaterial information to colliders.
            Refactored UI.
            Components can now be collapsed in the Inspector list.
            Mouse cursor can now be activated when entering GUI.

            Added options for mouse cursor (default false), 
            component default collapse state (default collapsed), 
            window width and height, 
            show public/non-public variables by default (default false for non-public, true for public).

            Added support for CharacterController- and Camera- component.
            Changed transform component functions, text boxes will no longer update in real-time, but can be applied or reverted with buttons.
            Transform component can now copy transform information to clipboard via button.
            Ability to change Copy and Paste Keybind
        */

        public override string ID => "DeveloperToolkit";
        public override string Name => "Developer Toolkit";
        public override string Author => "zamp, piotrulos, Fredrik";
        public override string Version => string.Format("{0}.{1}.{2}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Major, System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Minor, System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Build);
        public override bool UseAssetsFolder => false;
        public override bool LoadInMenu => true;

        public Keybind showGui = new Keybind("showgui", "Show GUI", KeyCode.Z, KeyCode.LeftControl);
        public Keybind copy = new Keybind("copy", "Copy", KeyCode.C, KeyCode.LeftControl);
        public Keybind paste = new Keybind("paste", "Paste", KeyCode.V, KeyCode.LeftControl);
        public Keybind raycastTweakable = new Keybind("raycastTweakable", "Raycast to Inspector", KeyCode.G, KeyCode.LeftControl);

        public static Settings menuMouse = new Settings("menuMouse", "Show mouse cursor when opening GUI", false);
        public static Settings componentCollapse = new Settings("componentCollapse", "Show components as collapsed by default", true, ResetCollapse);
        public static Settings hierarchyWidth = new Settings("hierarchyWidth", "Hierarchy window width", 600, ChangeDimensions);
        public static Settings hierarchyHeight = new Settings("hierarchyHeight", "Hierarchy window height", Screen.height - 5, ChangeDimensions);
        public static Settings inspectorWidth = new Settings("inspectorWidth", "Inspector window width", 400, ChangeDimensions);
        public static Settings inspectorHeight = new Settings("inspectorHeight", "Inspector window height", Screen.height - 5, ChangeDimensions);
        public static Settings variablesPublic = new Settings("variablesPublic", "Show public variables by default", true, VariableVisibility);
        public static Settings variablesPrivate = new Settings("variablesPrivate", "Show non-public variables by default", false, VariableVisibility);

        private Transform m_copy;
        private string m_copiedStr;
        private float m_copiedStrTime;

        public override void ModSettings()
        {
            Keybind.Add(this, showGui);
            Keybind.Add(this, raycastTweakable);
            Keybind.Add(this, copy);
            Keybind.Add(this, paste);

            Settings.AddHeader(this, "Basic Settings", new Color32(0, 128, 0, 255));
            Settings.AddCheckBox(this, menuMouse);
            Settings.AddCheckBox(this, componentCollapse);
            Settings.AddCheckBox(this, variablesPublic);
            Settings.AddCheckBox(this, variablesPrivate);
            Settings.AddHeader(this, "Window sizes", new Color32(0, 128, 0, 255));
            Settings.AddText(this, "Set inspector and hierarchy window size here");
            Settings.AddSlider(this, hierarchyWidth, 200, Screen.width);
            Settings.AddSlider(this, hierarchyHeight, 200, Screen.height);
            Settings.AddSlider(this, inspectorWidth, 200, Screen.width);
            Settings.AddSlider(this, inspectorHeight, 200, Screen.height);

        }

        public override void ModSettingsLoaded()
        {
            ChangeDimensions();
            VariableVisibility();
        }

        public override void OnMenuLoad()
        {
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
            {
                Inspector.showGUI = !Inspector.showGUI;

                if ((bool)menuMouse.GetValue() && Application.loadedLevelName == "GAME")
                    FsmVariables.GlobalVariables.FindFsmBool("PlayerInMenu").Value = Inspector.showGUI;
            }

            if (Camera.main != null && (raycastTweakable.IsPressed() || copy.IsDown() || paste.IsDown()))
            {
                Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit);
                if (hit.collider)
                {
                    if (raycastTweakable.IsPressed())
                        Inspector.SetInspect(hit.collider.transform);
                    else if (copy.IsDown())
                    {
                        m_copy = hit.collider.transform;
                        m_copiedStr = m_copy.name + " copied!";
                        m_copiedStrTime = 2f;
                    }
                    else if (paste.IsDown())
                    {
                        GameObject clone = GameObject.Instantiate(m_copy.gameObject);
                        clone.name = m_copy.gameObject.name;
                        clone.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 2f;
                    }
                }
            }
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

        static void ChangeDimensions()
        {
            Inspector.hierarchyWidth = int.Parse(hierarchyWidth.GetValue().ToString());
            Inspector.hierarchyHeight = int.Parse(hierarchyHeight.GetValue().ToString());
            Inspector.inspectorWidth = int.Parse(inspectorWidth.GetValue().ToString());
            Inspector.inspectorHeight = int.Parse(inspectorHeight.GetValue().ToString());
        }
        
        static void VariableVisibility()
        {
            Inspector.m_bindingFlagPublic = (bool)variablesPublic.GetValue();
            Inspector.m_bindingFlagNonPublic = (bool)variablesPrivate.GetValue();
        }

        static void ResetCollapse()
        {
            Inspector.componentToggle = new System.Collections.Generic.Dictionary<Component, bool>();
        }

        /*
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
        */
    }
}
