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
                        GUILayout.BeginArea(new Rect(Screen.width - 401, 0, 400, Screen.height));
                        m_inspectScrollPosition = GUILayout.BeginScrollView(m_inspectScrollPosition, false, true, GUILayout.Width(400));
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
                GUILayout.Label("<b>Layer: </b>" + LayerMask.LayerToName(trans.gameObject.layer) + " [" + trans.gameObject.layer + "]");
                GUILayout.Label("<b>Tag: </b>" + trans.gameObject.tag);
                GUILayout.EndHorizontal();
                if (GoPath != string.Empty)
                    GUILayout.TextField(GoPath, GUILayout.MaxWidth(400));
                GUILayout.BeginVertical("box");
                m_bindingFlagPublic = GUILayout.Toggle(m_bindingFlagPublic, "Show public variables");
                m_bindingFlagNonPublic = GUILayout.Toggle(m_bindingFlagNonPublic, "Show non-public variables");
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
                        case Transform _: TransformGUI(comp); break;
                        case Collider _: ColliderGUI(comp); break;
                        case AudioSource _: AudioSourceGUI(comp as AudioSource); break;
                        case MeshFilter _: MeshFilterGUI(comp as MeshFilter); break;
                        case MeshRenderer _: MeshRendererGUI(comp as MeshRenderer); break;
                        case Light _: LightGUI(comp as Light); break;
                        case SpringJoint _: SpringJointComp(comp as SpringJoint); break;
                        case Animation _: AnimationComp(comp as Animation); break;
                        case Rigidbody _: RigidbodyComp(comp as Rigidbody); break;
                        case TextMesh _: TextMeshComp(comp as TextMesh); break;
                        case HingeJoint _: HingeJointComp(comp as HingeJoint); break;
                        case FixedJoint _: FixedJointComp(comp as FixedJoint); break;
                        case SkinnedMeshRenderer _: SkinnedMeshRendererGUI(comp as SkinnedMeshRenderer); break;
                        case PlayMakerFSM _: FSMGUI(comp); break;
                        default: GenericsGUI(comp, flags); break;
                    }
                }
                GUILayout.EndVertical();
            }
        }

        private static void FixedJointComp(FixedJoint comp)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Connected Body:</b>");
            GUILayout.Label(comp.connectedBody.ToString());

            GUILayout.Label("<b>Break Force: </b>" + comp.breakForce);
            GUILayout.Label("<b>Break Torque: </b>" + comp.breakTorque);

            GUILayout.Label("<b>Enable Collision: </b>" + comp.enableCollision);
            GUILayout.Label("<b>Enable Preprocessing: </b>" + comp.enablePreprocessing);

            GUILayout.EndVertical();
        }

        private static void HingeJointComp(HingeJoint comp)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Connected Body:</b>");
            GUILayout.Label(comp.connectedBody.ToString());

            Vector3 anch = comp.anchor;
            GUILayout.Label("<b>Anchor:</b>");
            GUILayout.Label("<b>   X: </b>" + anch.x + "<b>, Y: </b>" + anch.y + "<b>, Z: </b>" + anch.z);

            Vector3 axis = comp.axis;
            GUILayout.Label("<b>Axis:</b>");
            GUILayout.Label("<b>   X: </b>" + axis.x + "<b>, Y: </b>" + axis.y + "<b>, Z: </b>" + axis.z);

            GUILayout.Label("<b>Auto Configure Connected Anchor: </b>" + comp.autoConfigureConnectedAnchor);
            Vector3 canch = comp.connectedAnchor;
            GUILayout.Label("<b>Connected Anchor:</b>");
            GUILayout.Label("<b>   X: </b>" + canch.x + "<b>, Y: </b>" + canch.y + "<b>, Z: </b>" + canch.z);

            GUILayout.Label("<b>Use Spring: </b>" + comp.useSpring);
            JointSpring spring = comp.spring;
            GUILayout.Label("<b>Spring: </b>");
            GUILayout.Label("<b>   Spring: </b>" + spring.spring);
            GUILayout.Label("<b>   Damper: </b>" + spring.damper);
            GUILayout.Label("<b>   Target Position: </b>" + spring.targetPosition);

            GUILayout.Label("<b>Use Motor: </b>" + comp.useMotor);
            JointMotor motor = comp.motor;
            GUILayout.Label("<b>Motor: </b>");
            GUILayout.Label("<b>   Target Velocity: </b>" + motor.targetVelocity);
            GUILayout.Label("<b>   Damper: </b>" + motor.force);
            GUILayout.Label("<b>   Free Spin: </b>" + motor.freeSpin);

            GUILayout.Label("<b>Use Limits: </b>" + comp.useLimits);
            JointLimits limits = comp.limits;
            GUILayout.Label("<b>Limits: </b>");
            GUILayout.Label("<b>   Min: </b>" + limits.min);
            GUILayout.Label("<b>   Max: </b>" + limits.max);
            GUILayout.Label("<b>   Min Bounce: </b>" + limits.minBounce);
            GUILayout.Label("<b>   Max Bounce: </b>" + limits.maxBounce);
            GUILayout.Label("<b>   Contact Distance: </b>" + limits.contactDistance);

            GUILayout.Label("<b>Break Force: </b>" + comp.breakForce);
            GUILayout.Label("<b>Break Torque: </b>" + comp.breakTorque);

            GUILayout.Label("<b>Enable Collision: </b>" + comp.enableCollision);
            GUILayout.Label("<b>Enable Preprocessing: </b>" + comp.enablePreprocessing);

            GUILayout.EndVertical();
        }

        private static void TextMeshComp(TextMesh textMesh)
        {
            int intValue;
            float floatValue;

            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Text:</b>");
            string text = textMesh.text;
            text = GUILayout.TextField(textMesh.text);
            textMesh.text = text;

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Offset Z:</b>");
            floatValue = textMesh.offsetZ;
            floatValue = (float)Convert.ToDouble(GUILayout.TextField(textMesh.offsetZ.ToString()));
            textMesh.offsetZ = floatValue;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Character Size:</b>");
            floatValue = textMesh.characterSize;
            floatValue = (float)Convert.ToDouble(GUILayout.TextField(textMesh.characterSize.ToString()));
            textMesh.characterSize = floatValue;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Line Spacing:</b>");
            floatValue = textMesh.lineSpacing;
            floatValue = (float)Convert.ToDouble(GUILayout.TextField(textMesh.lineSpacing.ToString()));
            textMesh.lineSpacing = floatValue;
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Anchor: </b>" + textMesh.anchor);
            GUILayout.BeginHorizontal();
            intValue = (int)textMesh.anchor;
            intValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(intValue, 0, 8));
            textMesh.anchor = (TextAnchor)intValue;
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Alignment: </b>" + textMesh.alignment);
            GUILayout.BeginHorizontal();
            intValue = (int)textMesh.alignment;
            intValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(intValue, 0, 2));
            textMesh.alignment = (TextAlignment)intValue;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Tab Size:</b>");
            floatValue = textMesh.tabSize;
            floatValue = (float)Convert.ToDouble(GUILayout.TextField(textMesh.tabSize.ToString()));
            textMesh.tabSize = floatValue;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Font Size:</b>");
            intValue = textMesh.fontSize;
            intValue = Convert.ToInt32(GUILayout.TextField(textMesh.fontSize.ToString()));
            textMesh.fontSize = intValue;
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Font Style: </b>" + textMesh.fontStyle);
            GUILayout.BeginHorizontal();
            intValue = (int)textMesh.fontStyle;
            intValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(intValue, 0, 3));
            textMesh.fontStyle = (FontStyle)intValue;
            GUILayout.EndHorizontal();

            bool boolValue = textMesh.richText;
            boolValue = GUILayout.Toggle(textMesh.richText, "Rich Text");
            textMesh.richText = boolValue;

            GUILayout.Label("<b>Font: </b>" + textMesh.font);

            GUILayout.Label("<b>Color: </b>" + textMesh.color);
            GUILayout.BeginHorizontal();
            Color color = textMesh.color;
            color.r = (float)Convert.ToDouble(GUILayout.TextField(color.r.ToString()));
            color.g = (float)Convert.ToDouble(GUILayout.TextField(color.g.ToString()));
            color.b = (float)Convert.ToDouble(GUILayout.TextField(color.b.ToString()));
            color.a = (float)Convert.ToDouble(GUILayout.TextField(color.a.ToString()));
            textMesh.color = color;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static void RigidbodyComp(Rigidbody rigidbody)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Mass: </b>" + rigidbody.mass);
            GUILayout.Label("<b>Drag: </b>" + rigidbody.drag);
            GUILayout.Label("<b>Angular Drag: </b>" + rigidbody.angularDrag);
            GUILayout.Label("<b>Use gravity: </b>" + rigidbody.useGravity);
            GUILayout.Label("<b>Is Kinematic: </b>" + rigidbody.isKinematic);
            GUILayout.Label("<b>Detect collisions: </b>" + rigidbody.detectCollisions);

            GUILayout.Label("<b>Interpolate: </b>" + rigidbody.interpolation);

            GUILayout.Label("<b>Collision Detection: </b>" + rigidbody.collisionDetectionMode);

            GUILayout.Label("<b>Velocity: </b>" + rigidbody.velocity.magnitude);
            GUILayout.Label("<b>Velocity Vector: </b>" + rigidbody.velocity);

            GUILayout.EndVertical();

            /*
            GUILayout.BeginVertical("box");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mass: ");
            rigidbody.mass = (float)Convert.ToDouble(GUILayout.TextField(rigidbody.mass.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Drag: ");
            rigidbody.mass = (float)Convert.ToDouble(GUILayout.TextField(rigidbody.drag.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Angular Drag: ");
            rigidbody.mass = (float)Convert.ToDouble(GUILayout.TextField(rigidbody.angularDrag.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.Toggle(rigidbody.useGravity, ": Use Gravity");
            GUILayout.Toggle(rigidbody.isKinematic, ": Is Kinematic");

            GUILayout.BeginHorizontal();
            string interpolation = "(None)";
            int interpolationValue = 0;
            if (rigidbody.interpolation == RigidbodyInterpolation.Interpolate)
            {
                interpolation = "(Interpolate)";
                interpolationValue = 1;
            }
            else if (rigidbody.interpolation == RigidbodyInterpolation.Extrapolate)
            {
                interpolation = "(Extrapolate)";
                interpolationValue = 2;
            }
            GUILayout.Label("Interpolate: " + interpolation);
            interpolationValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(interpolationValue, 0, 2));
            if (interpolationValue == 0) rigidbody.interpolation = RigidbodyInterpolation.None;
            else if (interpolationValue == 1) rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            else if (interpolationValue == 2) rigidbody.interpolation = RigidbodyInterpolation.Extrapolate;
            GUILayout.EndHorizontal();

            string collisionDetection = "(Discrete)";
            int collisionDetectionValue = 0;
            if (rigidbody.collisionDetectionMode == CollisionDetectionMode.Continuous)
            {
                collisionDetection = "(Continuous)";
                collisionDetectionValue = 1;
            }
            else if (rigidbody.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic)
            {
                collisionDetection = "(Continuous Dynamic)";
                collisionDetectionValue = 2;
            }
            GUILayout.Label("Collision Detection: " + collisionDetection);
            GUILayout.BeginHorizontal();
            collisionDetectionValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(collisionDetectionValue, 0, 2));
            if (collisionDetectionValue == 0) rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            else if (collisionDetectionValue == 1) rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            else if (collisionDetectionValue == 2) rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GUILayout.EndHorizontal();

            GUILayout.Toggle(rigidbody.detectCollisions, ": Detect Collisions");

            
            GUILayout.Label("Constraints: ");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Freeze Position: ");
            RigidbodyConstraints constraints = rigidbody.constraints;
            
            GUILayout.Toggle(constraints.None, ": Detect Collisions");
             = (float)Convert.ToDouble(GUILayout.TextField(rigidbody.angularDrag.ToString()));
            
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            */
        }

        private static void AnimationComp(Animation animation)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Animation clip: </b>" + animation.clip);
            bool firstLine = false;
            int index = 0;
            foreach (AnimationState state in animation)
            {
                if (!firstLine)
                {
                    firstLine = true;
                    GUILayout.Label("<b>Animation clips: </b>");
                }
                GUILayout.Label("   <b>Clip " + index + ": </b>" + state.clip);
                index++;
            }
            GUILayout.Label("<b>Play Automatically: </b>" + animation.playAutomatically);
            GUILayout.Label("<b>Animate Physics: </b>" + animation.animatePhysics);
            GUILayout.Label("<b>Culling Type: </b>" + animation.cullingType);

            GUILayout.EndVertical();

        }

        private static void SpringJointComp(SpringJoint comp)
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Connected Body:</b>");
            GUILayout.Label(comp.connectedBody.ToString());

            Vector3 vector3Value = comp.anchor;
            GUILayout.Label("<b>Anchor:</b>");
            GUILayout.Label("<b>   X: </b>" + vector3Value.x.ToString() + "<b>, Y: </b>" + vector3Value.y + "<b>, Z: </b>" + vector3Value.z);

            GUILayout.Label("<b>Auto Configure Connected Anchor: </b>" + comp.autoConfigureConnectedAnchor);
            vector3Value = comp.connectedAnchor;
            GUILayout.Label("<b>Connected Anchor:</b>");
            GUILayout.Label("<b>   X: </b>" + vector3Value.x + "<b>, Y: </b>" + vector3Value.y + "<b>, Z: </b>" + vector3Value.z);

            GUILayout.Label("<b>Spring: </b>" + comp.spring);
            GUILayout.Label("<b>Damper: </b>" + comp.damper);
            GUILayout.Label("<b>Min Distance: </b>" + comp.minDistance);
            GUILayout.Label("<b>Max Distance: </b>" + comp.maxDistance);

            GUILayout.Label("<b>Break Force: </b>" + comp.breakForce);
            GUILayout.Label("<b>Break Torque: </b>" + comp.breakTorque);

            GUILayout.Label("<b>Enable Collision: </b>" + comp.enableCollision);
            GUILayout.Label("<b>Enable Preprocessing: </b>" + comp.enablePreprocessing);

            GUILayout.EndVertical();
        }

        private static void MeshFilterGUI(MeshFilter meshFilter)
        {

            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Mesh: </b>" + meshFilter.mesh);
            GUILayout.Label("<b>Mesh Info: </b>");
            GUILayout.Label("<b>    Sub Mesh Count: </b>" + meshFilter.mesh.subMeshCount);
            GUILayout.Label("<b>    Bounds: </b>" + meshFilter.mesh.bounds);
            GUILayout.Label("<b>    Vertex Count: </b>" + meshFilter.mesh.vertexCount);
            GUILayout.Label("<b>    BlendShape Count: </b>" + meshFilter.mesh.blendShapeCount);

            GUILayout.Label("<b>Shared Mesh: </b>" + meshFilter.sharedMesh);
            GUILayout.Label("<b>Shared Mesh Info: </b>");
            GUILayout.Label("<b>    Sub Mesh Count: </b>" + meshFilter.sharedMesh.subMeshCount);
            GUILayout.Label("<b>    Bounds: </b>" + meshFilter.sharedMesh.bounds);
            GUILayout.Label("<b>    Vertex Count: </b>" + meshFilter.sharedMesh.vertexCount);
            GUILayout.Label("<b>    BlendShape Count: </b>" + meshFilter.sharedMesh.blendShapeCount);
            GUILayout.EndVertical();
        }

        static string[] textureTypes = new string[] { "_MainTex", "_BumpMap", "_SpecGlossMap", "_EmissionMap", "_Reflection", "_Cutoff", "_MetallicGlossMap", "_DETAIL_MULX2", "_METALLICGLOSSMAP", "_NORMALMAP", "_SPECGLOSSMAP", "_DetailNormalMap", "_ParallaxMap", "_OcclusionMap", "_DetailMask", "_DetailAlbedoMap" };
        private static void MeshRendererGUI(MeshRenderer meshRenderer)
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Cast Shadows: </b>" + meshRenderer.shadowCastingMode);
            GUILayout.Label("<b>Receive Shadows: </b>" + meshRenderer.receiveShadows);

            GUILayout.Label("<b>Materials:</b>");
            int i = 0;
            foreach (Material material in meshRenderer.sharedMaterials)
            {
                GUILayout.Label("<b>   Material " + i + ":</b>");
                i++;
                GUILayout.Label("<b>       Name: </b>" + material.name);
                GUILayout.Label("<b>       Textures: </b>");
                foreach (string type in textureTypes.Where(type => material.GetTexture(type) != null))
                    GUILayout.Label("<b>           " + type + ": </b>" + material.GetTexture(type).name);
                GUILayout.Label("<b>       Shader: </b>" + material.shader.name);
                GUILayout.Label("<b>       Color: </b>" + material.color);
            }

            GUILayout.Label("<b>Use Light Probes: </b>" + meshRenderer.useLightProbes);
            GUILayout.Label("<b>Reflection Probes: </b>" + meshRenderer.reflectionProbeUsage);
            GUILayout.Label("<b>Anchor Override: </b>" + meshRenderer.probeAnchor);
            GUILayout.Label("<b>Part Of Static Batch: </b>" + meshRenderer.isPartOfStaticBatch);

            GUILayout.EndVertical();
        }

        private static void SkinnedMeshRendererGUI(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Cast Shadows: </b>" + skinnedMeshRenderer.shadowCastingMode);

            GUILayout.Label("<b>Receive Shadows: </b>" + skinnedMeshRenderer.receiveShadows);

            GUILayout.Label("<b>Materials:</b>");
            int i = 0;
            foreach (Material material in skinnedMeshRenderer.sharedMaterials)
            {
                GUILayout.Label("<b>   Material" + i + ":</b>");
                i++;
                GUILayout.Label("<b>       Name: </b>" + material.name);
                GUILayout.Label("<b>       Textures: </b>");
                foreach (string type in textureTypes.Where(type => material.GetTexture(type) != null))
                    GUILayout.Label("<b>           " + type + ": </b>" + material.GetTexture(type).name);
                GUILayout.Label("<b>       Shader: </b>" + material.shader.name);
                GUILayout.Label("<b>       Color: </b>" + material.color);
            }

            GUILayout.Label("<b>Use Light Probes: </b>" + skinnedMeshRenderer.useLightProbes);
            GUILayout.Label("<b>Reflection Probes: </b>" + skinnedMeshRenderer.reflectionProbeUsage);
            GUILayout.Label("<b>Anchor Override: </b>" + skinnedMeshRenderer.probeAnchor);
            GUILayout.Label("<b>Quality: </b>" + skinnedMeshRenderer.quality);

            GUILayout.Label("<b>Update When Offscreen: </b>" + skinnedMeshRenderer.updateWhenOffscreen);
            GUILayout.Label("<b>Mesh: </b>" + skinnedMeshRenderer.sharedMesh);
            GUILayout.Label("<b>Root Bone: </b>" + skinnedMeshRenderer.rootBone);
            GUILayout.Label("<b>Bounds: </b>" + skinnedMeshRenderer.bounds);

            GUILayout.Label("<b>Part Of Static Batch: </b>" + skinnedMeshRenderer.isPartOfStaticBatch);

            GUILayout.EndVertical();
        }

        private static void AudioSourceGUI(AudioSource aud)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Audio Clip: </b>" + aud.clip);
            GUILayout.Label("<b>Output: </b>" + aud.outputAudioMixerGroup);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Mute:</b>");
            aud.mute = GUILayout.Toggle(aud.mute, "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Bypass Effects:</b>");
            aud.bypassEffects = GUILayout.Toggle(aud.bypassEffects, "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Bypass Listener Effects:</b>");
            aud.bypassListenerEffects = GUILayout.Toggle(aud.bypassListenerEffects, "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Bypass Reverb Zones:</b>");
            aud.bypassReverbZones = GUILayout.Toggle(aud.bypassReverbZones, "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Play On Awake:</b>");
            aud.playOnAwake = GUILayout.Toggle(aud.playOnAwake, "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Loop:</b>");
            aud.loop = GUILayout.Toggle(aud.loop, "");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Priority: </b>");
            aud.priority = Convert.ToInt32(GUILayout.TextField(aud.priority.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Volume: </b>");
            aud.volume = (float)Convert.ToDouble(GUILayout.TextField(aud.volume.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Pitch: </b>");
            aud.pitch = (float)Convert.ToDouble(GUILayout.TextField(aud.pitch.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Stereo Pan: </b>");
            aud.panStereo = (float)Convert.ToDouble(GUILayout.TextField(aud.panStereo.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Spatial Blend: </b>");
            aud.spatialBlend = (float)Convert.ToDouble(GUILayout.TextField(aud.spatialBlend.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Reverb zone mix: </b>");
            aud.reverbZoneMix = (float)Convert.ToDouble(GUILayout.TextField(aud.reverbZoneMix.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>3D Sound Settings: </b>");

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>    Doppler level: </b>");
            aud.dopplerLevel = (float)Convert.ToDouble(GUILayout.TextField(aud.dopplerLevel.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>    Volume Rolloff: </b>" + aud.rolloffMode);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>        Min Distance: </b>");
            aud.minDistance = (float)Convert.ToDouble(GUILayout.TextField(aud.minDistance.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>    Spread: </b>");
            aud.spread = (float)Convert.ToDouble(GUILayout.TextField(aud.spread.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>    Max distance: </b>");
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
                GUILayout.Label("<b>Convex: </b>" + col.convex);
                GUILayout.Label("<b>Is Trigger: </b>" + col.isTrigger);
                GUILayout.Label("<b>Physics Material: </b>" + col.sharedMaterial);
                GUILayout.Label("<b>Mesh: </b>" + col.sharedMesh);
            }
            else if (comp is BoxCollider)
            {
                BoxCollider col = comp as BoxCollider;
                GUILayout.Label("<b>Is Trigger: </b>" + col.isTrigger);
                GUILayout.Label("<b>Physics Material: </b>" + col.sharedMaterial);

                GUILayout.Label("<b>Center (X, Y, Z):</b>");
                GUILayout.BeginHorizontal();
                Vector3 pos = col.center;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                col.center = pos;
                GUILayout.EndHorizontal();

                GUILayout.Label("<b>Size (X, Y Z):</b>");
                GUILayout.BeginHorizontal();
                pos = col.size;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                col.size = pos;
                GUILayout.EndHorizontal();
            }
            else if (comp is SphereCollider)
            {
                SphereCollider col = comp as SphereCollider;
                GUILayout.Label("<b>Is Trigger: </b>" + col.isTrigger);
                GUILayout.Label("<b>Physics Material: </b>" + col.sharedMaterial);

                GUILayout.Label("<b>Center (X, Y, Z):</b>");
                GUILayout.BeginHorizontal();
                Vector3 pos = col.center;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                col.center = pos;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Radius: </b>");
                float radius = col.radius;
                radius = (float)Convert.ToDouble(GUILayout.TextField(col.radius.ToString()));
                col.radius = radius;
                GUILayout.EndHorizontal();
            }
            else if (comp is CapsuleCollider)
            {
                CapsuleCollider col = comp as CapsuleCollider;
                GUILayout.Label("<b>Is Trigger: </b>" + col.isTrigger);
                GUILayout.Label("<b>Physics Material: </b>" + col.sharedMaterial);

                GUILayout.Label("<b>Center (X, Y, Z):</b>");
                GUILayout.BeginHorizontal();
                Vector3 pos = col.center;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                col.center = pos;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Radius: </b>");
                float radius = col.radius;
                radius = (float)Convert.ToDouble(GUILayout.TextField(col.radius.ToString()));
                col.radius = radius;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Height: </b>");
                float height = col.height;
                height = (float)Convert.ToDouble(GUILayout.TextField(col.height.ToString()));
                col.height = height;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Direction: </b>" + new string[] { "X-Axis", "Y-axis", "Z-axis" }[col.direction]);
                col.direction = Mathf.RoundToInt(GUILayout.HorizontalSlider(col.direction, 0, 2));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private static void LightGUI(Light light)
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Type: </b>" + light.type);
            if (light.type == LightType.Spot || light.type == LightType.Point)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Range:</b>");
                light.range = (float)Convert.ToDouble(GUILayout.TextField(light.range.ToString()));
                GUILayout.EndHorizontal();
            }
            if (light.type == LightType.Spot)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Spot Angle:</b>");
                light.spotAngle = (float)Convert.ToDouble(GUILayout.TextField(light.spotAngle.ToString()));
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("<b>Color: (RGBA)</b>");
            GUILayout.BeginHorizontal();
            Color color = light.color;
            color.r = (float)Convert.ToDouble(GUILayout.TextField(color.r.ToString()));
            color.g = (float)Convert.ToDouble(GUILayout.TextField(color.g.ToString()));
            color.b = (float)Convert.ToDouble(GUILayout.TextField(color.b.ToString()));
            color.a = (float)Convert.ToDouble(GUILayout.TextField(color.a.ToString()));
            light.color = color;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Intensity: </b>");
            light.intensity = (float)Convert.ToDouble(GUILayout.TextField(light.intensity.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Bounce Intensity: </b>");
            light.bounceIntensity = (float)Convert.ToDouble(GUILayout.TextField(light.bounceIntensity.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Shadow Type: </b>" + light.shadows);
            int shadowType = (int)light.shadows;
            shadowType = Mathf.RoundToInt(GUILayout.HorizontalSlider(shadowType, 0, 2));
            light.shadows = (LightShadows)shadowType;
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Cookie: </b>" + light.cookie);
            if (light.type == LightType.Directional)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Cookie Size: </b>");
                light.cookieSize = (float)Convert.ToDouble(GUILayout.TextField(light.cookieSize.ToString()));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("<b>Flare: </b>" + light.flare);
            GUILayout.Label("<b>Render Mode: </b>" + light.renderMode);
            GUILayout.Label("<b>Culling Mask: </b>" + light.cullingMask);
            GUILayout.EndVertical();
        }

        private static void TransformGUI(Component comp)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            showLocalPosition = GUILayout.Toggle(showLocalPosition, "");
            GUILayout.Label("<b>Show local transform information</b>");
            GUILayout.EndHorizontal();
            Transform t = (Transform)comp;
            if (showLocalPosition)
            {
                GUILayout.Label("<b>Local Position (X, Y, Z):</b>");
                GUILayout.BeginHorizontal();
                Vector3 pos = t.localPosition;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                t.localPosition = pos;
                GUILayout.EndHorizontal();

                GUILayout.Label("<b>Local Rotation, Euler Angles (X, Y, Z):</b>");
                GUILayout.BeginHorizontal();
                pos = t.localEulerAngles;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                t.localEulerAngles = pos;
                GUILayout.EndHorizontal();

                GUILayout.Label("<b>Local Rotation, Quaternion (X, Y, Z, W): </b>");
                GUILayout.Label(t.localRotation.ToString());
            }
            else
            {
                GUILayout.Label("<b>Position (X, Y, Z):</b>");
                GUILayout.BeginHorizontal();
                Vector3 pos = t.position;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                t.position = pos;
                GUILayout.EndHorizontal();

                GUILayout.Label("<b>Rotation, Euler Angles (X, Y, Z):</b>");
                GUILayout.BeginHorizontal();
                pos = t.eulerAngles;
                pos.x = (float)Convert.ToDouble(GUILayout.TextField(pos.x.ToString()));
                pos.y = (float)Convert.ToDouble(GUILayout.TextField(pos.y.ToString()));
                pos.z = (float)Convert.ToDouble(GUILayout.TextField(pos.z.ToString()));
                t.eulerAngles = pos;
                GUILayout.EndHorizontal();

                GUILayout.Label("<b>Rotation, Quaternion (X, Y, Z, W): </b>" + t.rotation.ToString());
            }

            GUILayout.Label("<b>Local Scale:</b>");
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

        //This is to fix.
        private static void GenericsGUI(Component comp, BindingFlags flags)
        {
            MonoBehaviour mb = comp as MonoBehaviour;
            FieldInfo[] fields = comp.GetType().GetFields(flags | BindingFlags.Instance);
            GUILayout.BeginHorizontal();
            //GUILayout.Space(20);
            GUILayout.BeginVertical("box");

            try { GUILayout.Label("<b> Enabled: </b>" + mb.enabled.ToString()); }
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
                        GUILayout.Label("<b>" + fieldInfo.Name + ": </b>");
                        bool val = GUILayout.Toggle((bool)fieldValue, fieldInfo.Name);
                        fieldInfo.SetValue(comp, val);
                    }
                    else if (fieldValue is string)
                    {
                        GUILayout.Label("<b>" + fieldInfo.Name + ": </b>");
                        string val = GUILayout.TextField((string)fieldValue);
                        fieldInfo.SetValue(comp, val);
                    }
                    else if (fieldValue is int)
                    {
                        GUILayout.Label("<b>" + fieldInfo.Name + ": </b>");
                        int val = Convert.ToInt32(GUILayout.TextField(fieldValue.ToString()));
                        fieldInfo.SetValue(comp, val);
                    }
                    else if (fieldValue is float)
                    {
                        GUILayout.Label("<b>" + fieldInfo.Name + ": </b>");
                        float val = (float)Convert.ToDouble(GUILayout.TextField(fieldValue.ToString()));
                        fieldInfo.SetValue(comp, val);
                    }
                    else
                    {
                        GUILayout.Label("<b>" + fieldInfo.Name + ": </b>" + fieldValueStr);
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

            if (fsm.enabled) GUILayout.Label("Enabled");
            else GUILayout.Label("Disabled");

            if (fsm.Active) GUILayout.Label("Active");
            else GUILayout.Label("Not active");

            GUILayout.BeginHorizontal();
            //GUILayout.Space(20);
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>FSM Name: </b>" + fsm.Fsm.Name);

            SetFsmVarsFor(fsm, GUILayout.Toggle(ShowFsmVarsFor(fsm), "Show Variables"));
            if (ShowFsmVarsFor(fsm))
            {
                if (fsm.FsmVariables.FloatVariables.Count() > 0) ListFsmVariables(fsm.FsmVariables.FloatVariables);
                if (fsm.FsmVariables.IntVariables.Count() > 0) ListFsmVariables(fsm.FsmVariables.IntVariables);
                if (fsm.FsmVariables.BoolVariables.Count() > 0) ListFsmVariables(fsm.FsmVariables.BoolVariables);
                if (fsm.FsmVariables.StringVariables.Count() > 0) ListFsmVariables(fsm.FsmVariables.StringVariables);
                if (fsm.FsmVariables.Vector2Variables.Count() > 0) ListFsmVariables(fsm.FsmVariables.Vector2Variables);
                if (fsm.FsmVariables.Vector3Variables.Count() > 0) ListFsmVariables(fsm.FsmVariables.Vector3Variables);
                if (fsm.FsmVariables.RectVariables.Count() > 0) ListFsmVariables(fsm.FsmVariables.RectVariables);
                if (fsm.FsmVariables.QuaternionVariables.Count() > 0) ListFsmVariables(fsm.FsmVariables.QuaternionVariables);
                if (fsm.FsmVariables.ColorVariables.Count() > 0) ListFsmVariables(fsm.FsmVariables.ColorVariables);
                if (fsm.FsmVariables.GameObjectVariables.Count() > 0) ListFsmVariables(fsm.FsmVariables.GameObjectVariables);
                if (fsm.FsmVariables.MaterialVariables.Count() > 0) ListFsmMatVariables(fsm.FsmVariables.MaterialVariables);
                if (fsm.FsmVariables.TextureVariables.Count() > 0) ListFsmTexVariables(fsm.FsmVariables.TextureVariables);
                if (fsm.FsmVariables.ObjectVariables.Count() > 0) ListFsmVariables(fsm.FsmVariables.ObjectVariables);
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

        private static void ListFsmVariables(IEnumerable<FsmFloat> variables)
        {
            GUILayout.Label("<b>Float Variables (FsmFloat):</b>");
            foreach (FsmFloat fsmFloat in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmFloat.Name + ": </b>");
                fsmFloat.Value = (float)Convert.ToDouble(GUILayout.TextField(fsmFloat.Value.ToString()));
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmInt> variables)
        {
            GUILayout.Label("<b>Integer Variables (FsmInt):</b>");
            foreach (FsmInt fsmInt in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmInt.Name + ": </b>");
                fsmInt.Value = Convert.ToInt32(GUILayout.TextField(fsmInt.Value.ToString()));
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmBool> variables)
        {
            GUILayout.Label("<b>Boolean Variables (FsmBool):</b>");
            foreach (FsmBool fsmBool in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmBool.Name + ": </b>" + fsmBool.Value);
                fsmBool.Value = GUILayout.Toggle(fsmBool.Value, "");
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmString> variables)
        {
            GUILayout.Label("<b>String Variables (FsmString):</b>");
            foreach (FsmString fsmString in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmString.Name + ": </b>");
                fsmString.Value = GUILayout.TextField(fsmString.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmVector2> variables)
        {
            GUILayout.Label("<b>Vector2 Variables (FsmVector2):</b>");
            foreach (FsmVector2 fsmVector2 in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmVector2.Name + ": </b>" + fsmVector2.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmVector3> variables)
        {
            GUILayout.Label("<b>Vector3 Variables (FsmVector3):</b>");
            foreach (FsmVector3 fsmVector3 in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmVector3.Name + ": </b>" + fsmVector3.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmRect> variables)
        {
            GUILayout.Label("<b>Rect Variables (FsmRect):</b>");
            foreach (FsmRect fsmRect in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmRect.Name + ": </b>" + fsmRect.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmQuaternion> variables)
        {
            GUILayout.Label("<b>Quaternion Variables (FsmQuaternion):</b>");
            foreach (FsmQuaternion fsmQuaternion in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmQuaternion.Name + ": </b>" + fsmQuaternion.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmColor> variables)
        {
            GUILayout.Label("<b>Color Variables (FsmColor):</b>");
            foreach (FsmColor fsmColor in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmColor.Name + ": </b>" + fsmColor.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmGameObject> variables)
        {
            GUILayout.Label("<b>GameObject Variables (FsmGameObject):</b>");
            foreach (FsmGameObject fsmGameObject in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmGameObject.Name + ": </b>" + fsmGameObject.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmMatVariables(IEnumerable<FsmMaterial> variables)
        {
            GUILayout.Label("<b>Material Variables (FsmMaterial):</b>");
            foreach (FsmMaterial fsmMaterial in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmMaterial.Name + ": </b>" + fsmMaterial.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmTexVariables(IEnumerable<FsmTexture> variables)
        {
            GUILayout.Label("<b>Texture Variables (FsmTexture):</b>");
            foreach (FsmTexture fsmTexture in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmTexture.Name + ": </b>" + fsmTexture.Value);
                GUILayout.EndHorizontal();
            }
        }

        private static void ListFsmVariables(IEnumerable<FsmObject> variables)
        {
            GUILayout.Label("<b>Object Variables (FsmObject):</b>");
            foreach (FsmObject fsmObject in variables)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>   " + fsmObject.Name + ": </b>" + fsmObject.Value);
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
