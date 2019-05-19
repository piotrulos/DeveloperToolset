using HutongGames.PlayMaker;
using MSCLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DeveloperToolset
{
    public static class Inspector
    {
        public static bool showGUI;
        public static List<Transform> m_rootTransforms = new List<Transform>();
        private static Dictionary<Transform, bool> m_hierarchyOpen = new Dictionary<Transform, bool>();
        // private static Dictionary<Component, bool> showComp = new Dictionary<Component, bool>();

        private static bool caseSensitive;
        private static Vector2 m_hierarchyScrollPosition;
        private static Transform m_inspect;
        private static Vector2 m_inspectScrollPosition;
        private static string m_search = "";

        private readonly static Dictionary<PlayMakerFSM, FsmToggle> m_fsmToggles = new Dictionary<PlayMakerFSM, FsmToggle>();
        private static bool m_bindingFlagPublic;
        private static bool m_bindingFlagNonPublic;
        private static bool showLocalPosition = true;

        // public static Form1 form; //test external winform

        private class FsmToggle
        {
            public bool showVars;
            public bool showStates;
            public bool showEvents;
            public bool showGlobalTransitions;
        }

        internal static void Search(string keyword)
        {

            m_hierarchyOpen.Clear();
            // get all objs
            if (string.IsNullOrEmpty(keyword))
            {
                m_rootTransforms = Resources.FindObjectsOfTypeAll<Transform>().Where(x => x.parent == null).ToList();
            }
            else
            {
                if (caseSensitive)
                    m_rootTransforms = Resources.FindObjectsOfTypeAll<Transform>().Where(x => x.name.IndexOf(keyword) >= 0).ToList();
                else
                    m_rootTransforms = Resources.FindObjectsOfTypeAll<Transform>().Where(x => x.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            m_rootTransforms.Sort(TransformNameAscendingSort);
        }

        private static int TransformNameAscendingSort(Transform x, Transform y)
        {
            return string.Compare(x.name, y.name);
        }

        internal static void OnGUI()
        {
            //Some mods for whatever reason change GLOBAL settings for label, detect and change it back.
            GUIStyle centeredStyle = GUI.skin.GetStyle("Label");
            if (centeredStyle.alignment != TextAnchor.MiddleLeft) 
                centeredStyle.alignment = TextAnchor.MiddleLeft;
            try
            {
                if (showGUI)
                {
                    // show hierarchy
                    GUILayout.BeginArea(new Rect(0, 0, 600, Screen.height));
                    GUILayout.BeginVertical("box");
                    m_hierarchyScrollPosition = GUILayout.BeginScrollView(m_hierarchyScrollPosition, false, true, GUILayout.Width(598));

                    GUILayout.Label(string.Format("Hierarchy of: {0}", Application.loadedLevelName));
                    m_search = GUILayout.TextField(m_search);
                    if (GUILayout.Button("Search"))
                        Search(m_search);

                    caseSensitive = GUILayout.Toggle(caseSensitive, "Case sensitive");
                    foreach (Transform rootTransform in m_rootTransforms)
                    {
                        ShowHierarchy(rootTransform);
                    }
                    if(m_rootTransforms.Count == 0)
                    {
                        ShowHierarchy(null);
                    }
                    GUILayout.EndScrollView();
                    GUILayout.EndVertical();
                    GUILayout.EndArea();

                    if (m_inspect != null)
                    {
                        GUILayout.BeginArea(new Rect(Screen.width - 301, 0, 300, Screen.height));
                        m_inspectScrollPosition = GUILayout.BeginScrollView(m_inspectScrollPosition, false, true, GUILayout.Width(300));
                        GUILayout.Label(m_inspect.name);
                        if (GUILayout.Button("Close"))
                            m_inspect = null;
                        ShowInspect(m_inspect);
                        GUILayout.EndScrollView();
                        GUILayout.EndArea();
                    }
                }
            }
            catch (Exception e)
            {
                ModConsole.Print(e.ToString());
            }
        }

        private static void ShowInspect(Transform trans)
        {
            if (trans != null)
            {
                if (trans.parent != null && GUILayout.Button("Parent"))
                {
                    m_inspect = trans.parent;
                    return;
                }
                string GoPath = String.Empty;
                if (trans.parent != null)
                {
                    Transform t = trans;
                    GoPath = t.name;
                    while (t.parent != null)
                    {
                        t = t.parent.transform;
                        GoPath = GoPath.Insert(0, string.Format("{0}/", t.name));
                    }
                }

                trans.gameObject.SetActive(GUILayout.Toggle(trans.gameObject.activeSelf, "Is active"));
                GUILayout.BeginHorizontal("box");
                GUILayout.Label("Layer: " + LayerMask.LayerToName(trans.gameObject.layer) + " [" + trans.gameObject.layer + "]");
                GUILayout.Label("Tag: " + trans.gameObject.tag);
                GUILayout.EndHorizontal();
                if (GoPath != string.Empty)
                    GUILayout.TextField(GoPath, GUILayout.MaxWidth(300));
                GUILayout.BeginVertical("box");
                m_bindingFlagPublic = GUILayout.Toggle(m_bindingFlagPublic, "Show public");
                m_bindingFlagNonPublic = GUILayout.Toggle(m_bindingFlagNonPublic, "Show non-public");
                if (trans.gameObject.isStatic)
                {
                    GUILayout.Label("!!! This GameObject is static !!!");
                }

                BindingFlags flags = m_bindingFlagPublic ? BindingFlags.Public : BindingFlags.Default;
                flags |= m_bindingFlagNonPublic ? BindingFlags.NonPublic : BindingFlags.Default;

                foreach (Component comp in trans.GetComponents<Component>())
                {
                    Type type = comp.GetType();
                    //GUILayout.Label(type.ToString());
                    bool btn = GUILayout.Button(type.ToString());

                    switch (comp)
                    {
                        case Transform _:
                            TransformGUI(comp);
                            break;
                        case Collider _:
                            ColliderGUI(comp);
                            break;
                        case AudioSource _:
                            AudioSourceGUI(comp as AudioSource);
                            break;
                        case MeshFilter _:
                            MeshFilterGUI(comp as MeshFilter);
                            break;
                        case MeshRenderer _:
                            MeshRendererGUI(comp as MeshRenderer);
                            break;
                        case PlayMakerFSM _:
                            FSMGUI(comp);
                            break;
                        case Light _:
                            LightGUI(comp as Light);
                            break;
                        case SpringJoint _:
                            SpringJointComp(comp as SpringJoint);
                            break;
                        case Animation _:
                            AnimationComp(comp as Animation);
                            break;
                        case Rigidbody _:
                            RigidbodyComp(comp as Rigidbody);
                            break;
                        case TextMesh _:
                            TextMeshComp(comp as TextMesh);
                            break;
                        default:
                            GenericsGUI(comp, flags);
                            break;
                    }
                }
                GUILayout.EndVertical();
            }
        }

        private static void TextMeshComp(TextMesh textMesh)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Font: " + textMesh.font);
            GUILayout.Label("Text: " + textMesh.text);
            GUILayout.Label("Color: " + textMesh.color);
            GUILayout.Label("is Rich text: " + textMesh.richText);
            GUILayout.EndVertical();
        }

        private static void RigidbodyComp(Rigidbody rigidbody)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Mass: " + rigidbody.mass);
            GUILayout.Label("Drag: " + rigidbody.drag);
            GUILayout.Label("Angular Drag: " + rigidbody.angularDrag);
            GUILayout.Label("Use gravity: " + rigidbody.useGravity);
            GUILayout.Label("Is Kinematic: " + rigidbody.isKinematic);
            GUILayout.Label("Detect collisions: " + rigidbody.detectCollisions);
            GUILayout.EndVertical();
        }

        private static void AnimationComp(Animation animation)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Animation name: " + animation.name);
            GUILayout.Label("Animation clip: " + animation.clip);
            GUILayout.Label("Animation clip name: " + animation.clip.name);
            GUILayout.EndVertical();

        }

        private static void SpringJointComp(SpringJoint comp)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Connected body: " + comp.connectedBody);
            SpringJoint t = comp;

            GUILayout.Label("Anchor:");
            GUILayout.BeginHorizontal();
            Vector3 anch = t.anchor;
            anch.x = (float)Convert.ToDouble(GUILayout.TextField(anch.x.ToString()));
            anch.y = (float)Convert.ToDouble(GUILayout.TextField(anch.y.ToString()));
            anch.z = (float)Convert.ToDouble(GUILayout.TextField(anch.z.ToString()));
            t.anchor = anch;
            GUILayout.EndHorizontal();
            t.autoConfigureConnectedAnchor = GUILayout.Toggle(t.autoConfigureConnectedAnchor, "Auto Configure connected anchor");

            GUILayout.Label("Connected anchor:");
            GUILayout.BeginHorizontal();
            Vector3 canch = t.connectedAnchor;
            canch.x = (float)Convert.ToDouble(GUILayout.TextField(canch.x.ToString()));
            canch.y = (float)Convert.ToDouble(GUILayout.TextField(canch.y.ToString()));
            canch.z = (float)Convert.ToDouble(GUILayout.TextField(canch.z.ToString()));
            t.connectedAnchor = canch;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Spring:");
            t.spring = (float)Convert.ToDouble(GUILayout.TextField(t.spring.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Damper:");
            t.damper = (float)Convert.ToDouble(GUILayout.TextField(t.damper.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Min dist:");
            t.minDistance = (float)Convert.ToDouble(GUILayout.TextField(t.minDistance.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Max dist:");
            t.maxDistance = (float)Convert.ToDouble(GUILayout.TextField(t.maxDistance.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private static void MeshFilterGUI(MeshFilter mesh)
        {

            GUILayout.BeginVertical("box");
            GUILayout.Label("subMeshCount: " + mesh.mesh.subMeshCount);
            GUILayout.Label("name: " + mesh.name);
            GUILayout.Label("mesh: " + mesh.mesh);
            GUILayout.Label("sharedMesh: " + mesh.sharedMesh);
            GUILayout.EndVertical();
        }
        private static void MeshRendererGUI(MeshRenderer mr)
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label("isPartOfStaticBatch: " + mr.isPartOfStaticBatch);
            GUILayout.Label("Materal: " + mr.material);
            GUILayout.Label("Main Texture: " + mr.material.mainTexture);
            GUILayout.Label("Materal color: " + mr.material.color);
            GUILayout.EndVertical();
        }
        private static void AudioSourceGUI(AudioSource aud)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Audio Clip: " + aud.clip);
            GUILayout.Label("Output: " + aud.outputAudioMixerGroup);
            GUILayout.Toggle(aud.mute, "Mute");
            GUILayout.Toggle(aud.bypassEffects, "Bypass Effects");
            GUILayout.Toggle(aud.bypassListenerEffects, "Bypass Listener Effects");
            GUILayout.Toggle(aud.bypassReverbZones, "Bypass Reverb Zones");
            GUILayout.Toggle(aud.playOnAwake, "Play On Awake");
            GUILayout.Toggle(aud.loop, "Loop");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Priority: ");
            aud.priority = Convert.ToInt32(GUILayout.TextField(aud.priority.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Volume: ");
            aud.volume = (float)Convert.ToDouble(GUILayout.TextField(aud.volume.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Pitch: ");
            aud.pitch = (float)Convert.ToDouble(GUILayout.TextField(aud.pitch.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Stereo Pan: ");
            aud.panStereo = (float)Convert.ToDouble(GUILayout.TextField(aud.panStereo.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Spatial Blend: ");
            aud.spatialBlend = (float)Convert.ToDouble(GUILayout.TextField(aud.spatialBlend.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Reverb zone mix: ");
            aud.reverbZoneMix = (float)Convert.ToDouble(GUILayout.TextField(aud.reverbZoneMix.ToString()));
            GUILayout.EndHorizontal();
            GUILayout.Label("3D Sound Settings: ");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Doppler level: ");
            aud.dopplerLevel = (float)Convert.ToDouble(GUILayout.TextField(aud.dopplerLevel.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.Label("Volume Rollof: " + aud.rolloffMode);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Min Distance: ");
            aud.minDistance = (float)Convert.ToDouble(GUILayout.TextField(aud.minDistance.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Spread: ");
            aud.spread = (float)Convert.ToDouble(GUILayout.TextField(aud.spread.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Max distance: ");
            aud.maxDistance = (float)Convert.ToDouble(GUILayout.TextField(aud.maxDistance.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }
        private static void ColliderGUI(Component comp)
        {
            GUILayout.BeginVertical("box");
            if (comp is MeshCollider)
            {
                MeshCollider col = comp as MeshCollider;
                GUILayout.Toggle(col.convex, "Convex");
                GUILayout.Toggle(col.isTrigger, "Is Trigger");
                GUILayout.Label("Material: " + col.sharedMaterial);
                GUILayout.Label("Mesh: " + col.sharedMesh);
            }
            else
            {
                Collider col = comp as Collider;
                GUILayout.Toggle(col.isTrigger, "Is Trigger");
                GUILayout.Label("Material: " + col.sharedMaterial);

                if (comp is BoxCollider)
                {
                    GUILayout.Label("Center");
                    GUILayout.BeginHorizontal();
                    BoxCollider colB = comp as BoxCollider;
                    Vector3 pos = colB.center;
                    pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                    pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                    pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                    colB.center = pos;
                    GUILayout.EndHorizontal();

                    GUILayout.Label("Size");
                    GUILayout.BeginHorizontal();
                    pos = colB.size;
                    pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                    pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                    pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                    colB.size = pos;
                    GUILayout.EndHorizontal();
                }

            }
            GUILayout.EndVertical();
        }
        private static void LightGUI(Light light)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shadow bias:");
            light.shadowBias = (float)Convert.ToDouble(GUILayout.TextField(light.shadowBias.ToString()));
            GUILayout.EndHorizontal();
        }

        //This is to fix.
        private static void GenericsGUI(Component comp, BindingFlags flags)
        {
            MonoBehaviour mb = comp as MonoBehaviour;
            FieldInfo[] fields = comp.GetType().GetFields(flags | BindingFlags.Instance);
            GUILayout.BeginHorizontal();
            //GUILayout.Space(20);
            GUILayout.BeginVertical("box");
            try
            {
                GUILayout.Label(mb.enabled.ToString());
            }
            catch { /*catch the error spam to void */}
            foreach (FieldInfo fieldInfo in fields)
            {
                GUILayout.BeginHorizontal();
                try
                {
                    object fieldValue = fieldInfo.GetValue(comp);
                    string fieldValueStr = fieldValue.ToString();
                    if (fieldValue is bool)
                    {
                        GUILayout.Label(fieldInfo.Name);
                        bool val = GUILayout.Toggle((bool)fieldValue, fieldInfo.Name);
                        fieldInfo.SetValue(comp, val);
                    }
                    else if (fieldValue is string)
                    {
                        GUILayout.Label(fieldInfo.Name);
                        string val = GUILayout.TextField((string)fieldValue);
                        fieldInfo.SetValue(comp, val);
                    }
                    else if (fieldValue is int)
                    {
                        GUILayout.Label(fieldInfo.Name);
                        int val = Convert.ToInt32(GUILayout.TextField(fieldValue.ToString()));
                        fieldInfo.SetValue(comp, val);
                    }
                    else if (fieldValue is float)
                    {
                        GUILayout.Label(fieldInfo.Name);
                        float val = (float)Convert.ToDouble(GUILayout.TextField(fieldValue.ToString()));
                        fieldInfo.SetValue(comp, val);
                    }
                    else
                    {
                        GUILayout.Label(fieldInfo.Name + ": " + fieldValueStr);
                    }
                }
                catch (Exception)
                {
                    GUILayout.Label(fieldInfo.Name);
                }
                //fieldInfo.SetValue(fieldInfo.Name, GUILayout.TextField(fieldInfo.GetValue(fieldInfo.Name).ToString()));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void FSMGUI(Component comp)
        {
            PlayMakerFSM fsm = comp as PlayMakerFSM;
            if (fsm.enabled)
                GUILayout.Label("Enabled");
            else
                GUILayout.Label("Disabled");
            if (fsm.Active)
                GUILayout.Label("Active");
            else
                GUILayout.Label("Not active");



            GUILayout.BeginHorizontal();
            //GUILayout.Space(20);
            GUILayout.BeginVertical("box");

            GUILayout.Label("Name: " + fsm.Fsm.Name);

            SetFsmVarsFor(fsm, GUILayout.Toggle(ShowFsmVarsFor(fsm), "Show Variables"));
            if (ShowFsmVarsFor(fsm))
            {
                GUILayout.Label("Float");
                ListFsmVariables(fsm.FsmVariables.FloatVariables);
                GUILayout.Label("Int");
                ListFsmVariables(fsm.FsmVariables.IntVariables);
                GUILayout.Label("Bool");
                ListFsmVariables(fsm.FsmVariables.BoolVariables);
                GUILayout.Label("String");
                ListFsmVariables(fsm.FsmVariables.StringVariables);
                GUILayout.Label("Vector2");
                ListFsmVariables(fsm.FsmVariables.Vector2Variables);
                GUILayout.Label("Vector3");
                ListFsmVariables(fsm.FsmVariables.Vector3Variables);
                GUILayout.Label("Rect");
                ListFsmVariables(fsm.FsmVariables.RectVariables);
                GUILayout.Label("Quaternion");
                ListFsmVariables(fsm.FsmVariables.QuaternionVariables);
                GUILayout.Label("Color");
                ListFsmVariables(fsm.FsmVariables.ColorVariables);
                GUILayout.Label("GameObject");
                ListFsmVariables(fsm.FsmVariables.GameObjectVariables);
                GUILayout.Label("Material");
                ListFsmVariables(fsm.FsmVariables.MaterialVariables);
                GUILayout.Label("Texture");
                ListFsmVariables(fsm.FsmVariables.TextureVariables);
                GUILayout.Label("Object");
                ListFsmVariables(fsm.FsmVariables.ObjectVariables);
            }

            SetFsmGlobalTransitionFor(fsm, GUILayout.Toggle(ShowFsmGlobalTransitionFor(fsm), "Show Global Transition"));
            if (ShowFsmGlobalTransitionFor(fsm))
            {
                GUILayout.Space(20);
                GUILayout.Label("Global transitions");
                foreach (FsmTransition trans in fsm.FsmGlobalTransitions)
                {
                    GUILayout.Label("Event(" + trans.EventName + ") To State(" + trans.ToState + ")");
                }
            }

            SetFsmStatesFor(fsm, GUILayout.Toggle(ShowFsmStatesFor(fsm), "Show States"));
            if (ShowFsmStatesFor(fsm))
            {
                GUILayout.Space(20);
                GUILayout.Label("States");
                foreach (FsmState state in fsm.FsmStates)
                {
                    GUILayout.Label(state.Name + (fsm.ActiveStateName == state.Name ? "(active)" : ""));
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.BeginVertical("box");
                    GUILayout.Label("Transitions:");
                    foreach (FsmTransition transition in state.Transitions)
                    {
                        GUILayout.Label("Event(" + transition.EventName + ") To State(" + transition.ToState + ")");
                    }
                    GUILayout.Space(20);

                    GUILayout.Label("Actions:");
                    foreach (FsmStateAction action in state.Actions)
                    {
                        string typename = action.GetType().ToString();
                        typename = typename.Substring(typename.LastIndexOf(".", StringComparison.Ordinal) + 1);
                        GUILayout.Label(typename);

                        FieldInfo[] fields = action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.BeginVertical("box");
                        foreach (FieldInfo fieldInfo in fields)
                        {
                            GUILayout.BeginHorizontal();
                            try
                            {
                                object fieldValue = fieldInfo.GetValue(action);
                                string fieldValueStr = fieldValue.ToString();
                                fieldValueStr = fieldValueStr.Substring(fieldValueStr.LastIndexOf(".", System.StringComparison.Ordinal) + 1);
                                if (fieldValue is FsmProperty)
                                {
                                    FsmProperty property = fieldValue as FsmProperty;
                                    GUILayout.Label(fieldInfo.Name + ": (" + property.PropertyName + ")");
                                    GUILayout.Label("target: " + property.TargetObject + "");
                                }
                                else if (fieldValue is NamedVariable)
                                {
                                    NamedVariable named = fieldValue as NamedVariable;
                                    GUILayout.Label(fieldInfo.Name + ": " + fieldValueStr + "(" + named.Name + ")");
                                }
                                else if (fieldValue is FsmEvent)
                                {
                                    FsmEvent evnt = fieldValue as FsmEvent;
                                    GUILayout.Label(fieldInfo.Name + ": " + fieldValueStr + "(" + evnt.Name + ")");
                                }
                                else
                                {
                                    GUILayout.Label(fieldInfo.Name + ": " + fieldValueStr);
                                }
                            }
                            catch (Exception)
                            {
                                GUILayout.Label(fieldInfo.Name);
                            }
                            //fieldInfo.SetValue(fieldInfo.Name, GUILayout.TextField(fieldInfo.GetValue(fieldInfo.Name).ToString()));
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Label("ActionData:");
                    FieldInfo[] fields2 = state.ActionData.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.BeginVertical("box");
                    foreach (FieldInfo fieldInfo in fields2)
                    {
                        GUILayout.BeginHorizontal();
                        try
                        {
                            object fieldValue = fieldInfo.GetValue(state.ActionData);
                            string fieldValueStr = fieldValue.ToString();
                            fieldValueStr = fieldValueStr.Substring(fieldValueStr.LastIndexOf(".", System.StringComparison.Ordinal) + 1);
                            if (fieldValue is NamedVariable)
                            {
                                NamedVariable named = fieldValue as NamedVariable;
                                GUILayout.Label(fieldInfo.Name + ": " + fieldValueStr + "(" + named.Name + ")");
                            }
                            else if (fieldValue is FsmEvent)
                            {
                                FsmEvent evnt = fieldValue as FsmEvent;
                                GUILayout.Label(fieldInfo.Name + ": " + fieldValueStr + "(" + evnt.Name + ")");
                            }
                            else
                            {
                                GUILayout.Label(fieldInfo.Name + ": " + fieldValueStr);
                            }
                        }
                        catch (Exception)
                        {
                            GUILayout.Label(fieldInfo.Name);
                        }
                        //fieldInfo.SetValue(fieldInfo.Name, GUILayout.TextField(fieldInfo.GetValue(fieldInfo.Name).ToString()));
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
            }

            SetFsmEventsFor(fsm, GUILayout.Toggle(ShowFsmEventsFor(fsm), "Show Events"));
            if (ShowFsmEventsFor(fsm))
            {
                GUILayout.Space(20);
                GUILayout.Label("Events");
                foreach (FsmEvent evnt in fsm.FsmEvents)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(evnt.Name + ": " + evnt.Path);
                    if (GUILayout.Button("Send"))
                    {
                        fsm.SendEvent(evnt.Name);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void SetFsmGlobalTransitionFor(PlayMakerFSM fsm, bool p)
        {
            if (!m_fsmToggles.ContainsKey(fsm))
                m_fsmToggles.Add(fsm, new FsmToggle());
            m_fsmToggles[fsm].showGlobalTransitions = p;
        }

        private static bool ShowFsmGlobalTransitionFor(PlayMakerFSM fsm)
        {
            if (!m_fsmToggles.ContainsKey(fsm))
                m_fsmToggles.Add(fsm, new FsmToggle());
            return m_fsmToggles[fsm].showGlobalTransitions;
        }


        private static void SetFsmStatesFor(PlayMakerFSM fsm, bool p)
        {
            if (!m_fsmToggles.ContainsKey(fsm))
                m_fsmToggles.Add(fsm, new FsmToggle());
            m_fsmToggles[fsm].showStates = p;
        }

        private static bool ShowFsmStatesFor(PlayMakerFSM fsm)
        {
            if (!m_fsmToggles.ContainsKey(fsm))
                m_fsmToggles.Add(fsm, new FsmToggle());
            return m_fsmToggles[fsm].showStates;
        }

        private static void SetFsmEventsFor(PlayMakerFSM fsm, bool p)
        {
            if (!m_fsmToggles.ContainsKey(fsm))
                m_fsmToggles.Add(fsm, new FsmToggle());
            m_fsmToggles[fsm].showEvents = p;
        }

        private static bool ShowFsmEventsFor(PlayMakerFSM fsm)
        {
            if (!m_fsmToggles.ContainsKey(fsm))
                m_fsmToggles.Add(fsm, new FsmToggle());
            return m_fsmToggles[fsm].showEvents;
        }

        private static void SetFsmVarsFor(PlayMakerFSM fsm, bool p)
        {
            if (!m_fsmToggles.ContainsKey(fsm))
                m_fsmToggles.Add(fsm, new FsmToggle());
            m_fsmToggles[fsm].showVars = p;
        }

        private static bool ShowFsmVarsFor(PlayMakerFSM fsm)
        {
            if (!m_fsmToggles.ContainsKey(fsm))
                m_fsmToggles.Add(fsm, new FsmToggle());
            return m_fsmToggles[fsm].showVars;
        }

        private static void TransformGUI(Component comp)
        {
            GUILayout.BeginVertical("box");
            showLocalPosition = GUILayout.Toggle(showLocalPosition, "Show local");
            Transform t = (Transform)comp;
            if (showLocalPosition)
            {
                GUILayout.Label("localPosition:");
                GUILayout.BeginHorizontal();
                Vector3 pos = t.localPosition;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                t.localPosition = pos;
                GUILayout.EndHorizontal();

                GUILayout.Label("localRotation:");
                GUILayout.BeginHorizontal();
                pos = t.localRotation.eulerAngles;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                t.localRotation = Quaternion.Euler(pos);
                GUILayout.EndHorizontal();


            }
            else
            {
                GUILayout.Label("Position:");
                GUILayout.BeginHorizontal();
                Vector3 pos = t.position;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                t.position = pos;
                GUILayout.EndHorizontal();

                GUILayout.Label("Rotation:");
                GUILayout.BeginHorizontal();
                pos = t.rotation.eulerAngles;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                t.rotation = Quaternion.Euler(pos);
                GUILayout.EndHorizontal();

            }
            GUILayout.Label("localScale:");
            GUILayout.BeginHorizontal();
            Vector3 scl = t.localScale;
            scl.x = (float)Convert.ToDouble(GUILayout.TextField(scl.x.ToString()));
            scl.y = (float)Convert.ToDouble(GUILayout.TextField(scl.y.ToString()));
            scl.z = (float)Convert.ToDouble(GUILayout.TextField(scl.z.ToString()));
            t.localScale = scl;
            t.gameObject.isStatic = false;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static void ListFsmVariables(IEnumerable<FsmFloat> variables)
        {
            foreach (FsmFloat fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name);
                fsmFloat.Value = (float)Convert.ToDouble(GUILayout.TextField(fsmFloat.Value.ToString()));
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmBool> variables)
        {
            foreach (FsmBool fsmBool in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmBool.Name + ": " + fsmBool.Value);
                fsmBool.Value = GUILayout.Toggle(fsmBool.Value, "");
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmString> variables)
        {
            foreach (FsmString fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name);
                fsmFloat.Value = GUILayout.TextField(fsmFloat.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmInt> variables)
        {
            foreach (FsmInt fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name);
                fsmFloat.Value = Convert.ToInt32(GUILayout.TextField(fsmFloat.Value.ToString()));
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmColor> variables)
        {
            foreach (FsmColor fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name + ": " + fsmFloat.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmGameObject> variables)
        {
            foreach (FsmGameObject fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name + ": " + fsmFloat.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmVector2> variables)
        {
            foreach (FsmVector2 fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name + ": " + fsmFloat.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmVector3> variables)
        {
            foreach (FsmVector3 fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name + ": " + fsmFloat.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmRect> variables)
        {
            foreach (FsmRect fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name + ": " + fsmFloat.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmQuaternion> variables)
        {
            foreach (FsmQuaternion fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name + ": " + fsmFloat.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmObject> variables)
        {
            foreach (FsmObject fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(fsmFloat.Name + ": " + fsmFloat.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ShowHierarchy(Transform trans, int child = -1)
        {
            if (trans == null)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label("<color=orange><i>Nothing found...</i></color>");
                GUILayout.EndHorizontal();
                return;
            }
            if (trans.name == null || trans.name == string.Empty)
                return;

            if (!m_hierarchyOpen.ContainsKey(trans))
                m_hierarchyOpen.Add(trans, false);

            GUILayout.BeginHorizontal("box");

            if (trans.gameObject.activeSelf)
            {

                if (child == -1)
                {
                    GUILayout.Label(trans.name);
                }
                else
                {
                    GUILayout.Label(string.Format("<color=orange>[{0}]</color> {1}", child, trans.name));
                }
            }
            else
            {
                if (child == -1)
                {
                    GUILayout.Label(string.Format("<color=grey>{0}</color>",trans.name));
                }
                else
                {
                    GUILayout.Label(string.Format("<color=orange>[{0}]</color> <color=grey>{1}</color>", child, trans.name));
                }
            }

            if (GUILayout.Button("i", GUILayout.Width(20)))
            {
                m_inspect = trans;
            }
            bool btn = GUILayout.Button(m_hierarchyOpen[trans] ? "<" : ">", GUILayout.Width(20));
            if (m_hierarchyOpen[trans] && btn)
                m_hierarchyOpen[trans] = false;
            else if (!m_hierarchyOpen[trans] && btn)
                m_hierarchyOpen[trans] = true;

            GUILayout.EndHorizontal();

            if (m_hierarchyOpen[trans])
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Space(20);
                GUILayout.BeginVertical();


                //foreach (Transform t in trans)
                //child count
                for (int i = 0; i < trans.childCount; i++)
                {                   
                    ShowHierarchy(trans.GetChild(i),i);
                }
                if (trans.childCount == 0)
                {
                    GUILayout.Label("<color=brown><i>No child objects...</i></color>");
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        internal static void SetInspect(Transform transform)
        {
            m_inspect = transform;
            m_fsmToggles.Clear();
        }
    }
}
