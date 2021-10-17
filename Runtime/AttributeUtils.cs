using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditorInternal;
#endif

namespace BennyKok.CombatSystem
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class FoldoutAttribute : Attribute
    {
        public string group;
        public bool isActiveToggle;

        public FoldoutAttribute(string group)
        {
            this.group = group;
        }
        public FoldoutAttribute(string group, bool isActiveToggle) : this(group)
        {
            this.isActiveToggle = isActiveToggle;
        }
    }

#if UNITY_EDITOR
    public class FoldoutEditor : Editor
    {
        private Dictionary<string, FoldoutGroup> foldoutFields = new Dictionary<string, FoldoutGroup>();
        private string[] excludeList = new string[] { "m_Script" };
        private int propertyCount;

        public static Dictionary<int, bool> foldoutState = new Dictionary<int, bool>();

        public class FoldoutGroup
        {
            public List<FoldoutItem> items = new List<FoldoutItem>();
            public AnimBool visible;
            public SerializedProperty activeToggleProperty;
        }

        public struct FoldoutItem
        {
            public FoldoutAttribute attribute;
            public FieldInfo info;
            public SerializedProperty property;
        }

        public void OnBeforeAssemblyReload()
        {
            SessionState.SetIntArray("m_foldout_state_key", foldoutState.Keys.ToArray());
            SessionState.SetIntArray("m_foldout_state_values", foldoutState.Values.Select(x => x ? 1 : 0).ToArray());
            // Debug.Log("Before Assembly Reload");
        }

        public void OnAfterAssemblyReload()
        { 
            // Debug.Log("After Assembly Reload"); 
            var keys = SessionState.GetIntArray("m_foldout_state_key", null);
            var values = SessionState.GetIntArray("m_foldout_state_values", null);
            foldoutState.Clear();
            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    foldoutState.Add(keys[i], values[i] == 1);
                }
            }
        }
        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
        }

        protected virtual void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            propertyCount = 0;
            List<string> excludeListTemp = new List<string>() { "m_Script" };
            var iter = serializedObject.GetIterator();
            iter.NextVisible(true);
            do
            {
                propertyCount++;
                var info = target.GetType().GetField(iter.name);
                // Debug.Log($"{iter.name} {info}");
                if (info == null) continue;
                FoldoutAttribute foldoutAttribute = info.GetCustomAttribute<FoldoutAttribute>();
                // Debug.Log($"{info.Name} {foldoutAttribute}");
                if (foldoutAttribute != null)
                {
                    FoldoutGroup value;
                    if (foldoutFields.ContainsKey(foldoutAttribute.group))
                    {
                        value = foldoutFields[foldoutAttribute.group];
                    }
                    else
                    {
                        AnimBool animBool = new AnimBool(this.Repaint);
                        animBool.speed = 10;
                        value = new FoldoutGroup()
                        {
                            visible = animBool
                        };
                        foldoutFields.Add(foldoutAttribute.group, value);
                    }
                    // serializedObject.FindProperty(info.Name)
                    SerializedProperty serializedProperty = iter.Copy();
                    if (foldoutAttribute.isActiveToggle && value.activeToggleProperty == null)
                    {
                        value.activeToggleProperty = serializedProperty;
                    }
                    else
                    {
                        value.items.Add(new FoldoutItem()
                        {
                            attribute = foldoutAttribute,
                            info = info,
                            property = serializedProperty
                        });
                    }

                    var firstProperty = value.items.First();
                    if (foldoutState.TryGetValue(firstProperty.property.propertyPath.GetHashCode(), out var isOpen))
                        value.visible.value = isOpen;
                    else
                        foldoutState.Add(firstProperty.property.propertyPath.GetHashCode(), false);

                    excludeListTemp.Add(info.Name);
                }
            } while (iter.NextVisible(false));
            excludeList = excludeListTemp.ToArray();
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (excludeList.Length != propertyCount)
            {
                DrawPropertiesExcluding(serializedObject, excludeList);
                EditorGUILayout.Space();
            }
            DrawSplitter();
            foreach (var entry in foldoutFields)
            {
                var firstProperty = entry.Value.items.First();

                int key = firstProperty.property.propertyPath.GetHashCode();

                if (!foldoutState.TryGetValue(key, out var isOpen))
                    foldoutState.Add(key, false);

                EditorGUI.BeginChangeCheck();
                isOpen = DrawHeaderToggle(new GUIContent(entry.Key), isOpen, entry.Value.activeToggleProperty);

                foldoutState[key] = isOpen;
                if (entry.Value.visible.target != isOpen)
                    entry.Value.visible.target = isOpen;

                using (new EditorGUI.DisabledScope(entry.Value.activeToggleProperty != null && !entry.Value.activeToggleProperty.boolValue))
                {
                    using (new EditorGUILayout.FadeGroupScope(entry.Value.visible.faded))
                    {
                        if (entry.Value.visible.faded > 0)
                            foreach (var fields in entry.Value.items)
                                EditorGUILayout.PropertyField(fields.property);
                    }
                }
                DrawSplitter();
            }
            serializedObject.ApplyModifiedProperties();
        }
        class Styles
        {
            static readonly Color k_Normal_AllTheme = new Color32(0, 0, 0, 0);
            //static readonly Color k_Hover_Dark = new Color32(70, 70, 70, 255);
            //static readonly Color k_Hover = new Color32(193, 193, 193, 255);
            static readonly Color k_Active_Dark = new Color32(80, 80, 80, 255);
            static readonly Color k_Active = new Color32(216, 216, 216, 255);

            static readonly int s_MoreOptionsHash = "MoreOptions".GetHashCode();

            static public GUIContent moreOptionsLabel { get; private set; }
            static public GUIStyle moreOptionsStyle { get; private set; }
            static public GUIStyle moreOptionsLabelStyle { get; private set; }

            static Styles()
            {
                moreOptionsLabel = EditorGUIUtility.TrIconContent("MoreOptions", "More Options");

                moreOptionsStyle = new GUIStyle(GUI.skin.toggle);
                Texture2D normalColor = new Texture2D(1, 1);
                normalColor.SetPixel(1, 1, k_Normal_AllTheme);
                moreOptionsStyle.normal.background = normalColor;
                moreOptionsStyle.onActive.background = normalColor;
                moreOptionsStyle.onFocused.background = normalColor;
                moreOptionsStyle.onNormal.background = normalColor;
                moreOptionsStyle.onHover.background = normalColor;
                moreOptionsStyle.active.background = normalColor;
                moreOptionsStyle.focused.background = normalColor;
                moreOptionsStyle.hover.background = normalColor;

                moreOptionsLabelStyle = new GUIStyle(GUI.skin.label);
                moreOptionsLabelStyle.padding = new RectOffset(0, 0, 0, -1);
            }

            //Note:
            // - GUIStyle seams to be broken: all states have same state than normal light theme
            // - Hover with event will not be updated right when we enter the rect
            //-> Removing hover for now. Keep theme color for refactoring with UIElement later
            static public bool DrawMoreOptions(Rect rect, bool active)
            {
                int id = GUIUtility.GetControlID(s_MoreOptionsHash, FocusType.Passive, rect);
                var evt = Event.current;
                switch (evt.type)
                {
                    case EventType.Repaint:
                        Color background = k_Normal_AllTheme;
                        if (active)
                            background = EditorGUIUtility.isProSkin ? k_Active_Dark : k_Active;
                        EditorGUI.DrawRect(rect, background);
                        GUI.Label(rect, moreOptionsLabel, moreOptionsLabelStyle);
                        break;
                    case EventType.KeyDown:
                        bool anyModifiers = (evt.alt || evt.shift || evt.command || evt.control);
                        if ((evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && !anyModifiers && GUIUtility.keyboardControl == id)
                        {
                            evt.Use();
                            GUI.changed = true;
                            return !active;
                        }
                        break;
                    case EventType.MouseDown:
                        if (rect.Contains(evt.mousePosition))
                        {
                            GrabMouseControl(id);
                            evt.Use();
                        }
                        break;
                    case EventType.MouseUp:
                        if (HasMouseControl(id))
                        {
                            ReleaseMouseControl();
                            evt.Use();
                            if (rect.Contains(evt.mousePosition))
                            {
                                GUI.changed = true;
                                return !active;
                            }
                        }
                        break;
                    case EventType.MouseDrag:
                        if (HasMouseControl(id))
                            evt.Use();
                        break;
                }

                return active;
            }

            static int s_GrabbedID = -1;
            static void GrabMouseControl(int id) => s_GrabbedID = id;
            static void ReleaseMouseControl() => s_GrabbedID = -1;
            static bool HasMouseControl(int id) => s_GrabbedID == id;
        }
        /// <summary>Class containing style definition</summary>
        public static class CoreEditorStyles
        {
            /// <summary>Style for a small checkbox</summary>
            public static readonly GUIStyle smallTickbox;
            /// <summary>Style for a small checkbox in mixed state</summary>
            public static readonly GUIStyle smallMixedTickbox;
            /// <summary>Style for a minilabel button</summary>
            public static readonly GUIStyle miniLabelButton;

            static readonly Texture2D paneOptionsIconDark;
            static readonly Texture2D paneOptionsIconLight;

            /// <summary> PaneOption icon </summary>
            public static Texture2D paneOptionsIcon { get { return EditorGUIUtility.isProSkin ? paneOptionsIconDark : paneOptionsIconLight; } }

            static CoreEditorStyles()
            {
                smallTickbox = new GUIStyle("ShurikenToggle");
                smallMixedTickbox = new GUIStyle("ShurikenToggleMixed");

                var transparentTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                transparentTexture.SetPixel(0, 0, Color.clear);
                transparentTexture.Apply();

                miniLabelButton = new GUIStyle(EditorStyles.miniLabel);
                miniLabelButton.normal = new GUIStyleState
                {
                    background = transparentTexture,
                    scaledBackgrounds = null,
                    textColor = Color.grey
                };
                var activeState = new GUIStyleState
                {
                    background = transparentTexture,
                    scaledBackgrounds = null,
                    textColor = Color.white
                };
                miniLabelButton.active = activeState;
                miniLabelButton.onNormal = activeState;
                miniLabelButton.onActive = activeState;

                paneOptionsIconDark = (Texture2D)EditorGUIUtility.Load("Builtin Skins/DarkSkin/Images/pane options.png");
                paneOptionsIconLight = (Texture2D)EditorGUIUtility.Load("Builtin Skins/LightSkin/Images/pane options.png");
            }
        }

        /// <summary>Draw a splitter separator</summary>
        /// <param name="isBoxed">[Optional] add margin if the splitter is boxed</param>
        public static void DrawSplitter(bool isBoxed = false)
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f);
            float xMin = rect.xMin;

            // Splitter rect should be full-width
            rect.xMin = 0f;
            rect.width += 4f;

            if (isBoxed)
            {
                rect.xMin = xMin == 7.0 ? 4.0f : EditorGUIUtility.singleLineHeight;
                rect.width -= 1;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, !EditorGUIUtility.isProSkin
                ? new Color(0.6f, 0.6f, 0.6f, 1.333f)
                : new Color(0.12f, 0.12f, 0.12f, 1.333f));
        }

        /// <summary>Draw a header toggle like in Volumes</summary>
        /// <param name="title"> The title of the header </param>
        /// <param name="group"> The group of the header </param>
        /// <param name="activeField">The active field</param>
        /// <param name="contextAction">The context action</param>
        /// <param name="hasMoreOptions">Delegate saying if we have MoreOptions</param>
        /// <param name="toggleMoreOptions">Callback called when the MoreOptions is toggled</param>
        /// <param name="documentationURL">Documentation URL</param>
        /// <returns>return the state of the foldout header</returns>
        public static bool DrawHeaderToggle(GUIContent title, bool group, SerializedProperty activeField = null, Action<Vector2> contextAction = null, Func<bool> hasMoreOptions = null, Action toggleMoreOptions = null, string documentationURL = null)
        {
            var backgroundRect = GUILayoutUtility.GetRect(1f, 17f);

            var labelRect = backgroundRect;
            labelRect.xMin += 32f;
            labelRect.xMax -= 20f + 16 + 5;

            var foldoutRect = backgroundRect;
            foldoutRect.y += 1f;
            foldoutRect.width = 13f;
            foldoutRect.height = 13f;

            var toggleRect = backgroundRect;
            toggleRect.x += 16f;
            toggleRect.y += 2f;
            toggleRect.width = 13f;
            toggleRect.height = 13f;

            // More options 1/2
            var moreOptionsRect = new Rect();
            if (hasMoreOptions != null)
            {
                moreOptionsRect = backgroundRect;

                moreOptionsRect.x += moreOptionsRect.width - 16 - 1 - 16 - 5;

                if (!string.IsNullOrEmpty(documentationURL))
                    moreOptionsRect.x -= 16 + 7;

                moreOptionsRect.height = 15;
                moreOptionsRect.width = 16;
            }

            // Background rect should be full-width
            backgroundRect.xMin = 0f;
            backgroundRect.width += 4f;

            // Background
            float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
            EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

            if (activeField == null)
            {
                labelRect.xMin -= 13f;
            }

            // Title
            using (new EditorGUI.DisabledScope(activeField != null && !activeField.boolValue))
                EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

            // Foldout
            group = GUI.Toggle(foldoutRect, group, GUIContent.none, EditorStyles.foldout);

            // Active checkbox
            if (activeField != null)
            {
                // activeField.serializedObject.Update();
                activeField.boolValue = GUI.Toggle(toggleRect, activeField.boolValue, GUIContent.none, CoreEditorStyles.smallTickbox);
                // activeField.serializedObject.ApplyModifiedProperties();
            }

            // More options 2/2
            if (hasMoreOptions != null)
            {
                bool moreOptions = hasMoreOptions();
                bool newMoreOptions = Styles.DrawMoreOptions(moreOptionsRect, moreOptions);
                if (moreOptions ^ newMoreOptions)
                    toggleMoreOptions?.Invoke();
            }

            // Context menu
            var menuIcon = CoreEditorStyles.paneOptionsIcon;
            var menuRect = new Rect(labelRect.xMax + 3f + 16 + 5, labelRect.y + 1f, menuIcon.width, menuIcon.height);

            if (contextAction != null)
                GUI.DrawTexture(menuRect, menuIcon);

            // Documentation button
            if (!String.IsNullOrEmpty(documentationURL))
            {
                var documentationRect = menuRect;
                documentationRect.x -= 16 + 5;
                documentationRect.y -= 1;

                var documentationTooltip = $"Open Reference for {title.text}.";
                var documentationIcon = new GUIContent(EditorGUIUtility.TrIconContent("_Help").image, documentationTooltip);
                var documentationStyle = new GUIStyle("IconButton");

                if (GUI.Button(documentationRect, documentationIcon, documentationStyle))
                    System.Diagnostics.Process.Start(documentationURL);
            }

            // Handle events
            var e = Event.current;

            if (e.type == EventType.MouseDown)
            {
                if (contextAction != null && menuRect.Contains(e.mousePosition))
                {
                    contextAction(new Vector2(menuRect.x, menuRect.yMax));
                    e.Use();
                }
                else if (labelRect.Contains(e.mousePosition))
                {
                    if (e.button == 0)
                        group = !group;
                    else if (contextAction != null)
                        contextAction(e.mousePosition);

                    e.Use();
                }
            }

            return group;
        }
    }
#endif

    /// <summary>
    /// Wrapper class for our custom reorderable drawer
    /// </summary>
    /// <typeparam name="U">List Type</typeparam>
    [System.Serializable]
    public class ReorderableList<U> : ReorderableBase, IEnumerable<U>
    {
        public List<U> values;

        public U this[int index] { get => values[index]; set => values[index] = value; }

        public int Count => values.Count;

        public IEnumerator<U> GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => values.GetEnumerator();
    }

    public class ReorderableDisplayAttribute : PropertyAttribute
    {
        public string header;

        public ReorderableDisplayAttribute(string header)
        {
            this.header = header;
        }
    }

    [System.Serializable]
    public class ReorderableBase { }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReorderableBase), true)]
    public class ReorderableDrawer : PropertyDrawer
    {
        private ReorderableList list;

        public override void OnGUI(Rect rect, SerializedProperty serializedProperty, GUIContent label)
        {
            rect = EditorGUI.IndentedRect(rect);
            rect.y += 4;
            SerializedProperty listProperty = serializedProperty.FindPropertyRelative("values");

            GetList(serializedProperty.displayName, listProperty, label);

            float height = 0f;
            for (var i = 0; i < listProperty.arraySize; i++)
            {
                height = Mathf.Max(height, EditorGUI.GetPropertyHeight(listProperty.GetArrayElementAtIndex(i)));
            }
            list.elementHeight = height;
            list.DoList(rect);
        }

        public override float GetPropertyHeight(SerializedProperty serializedProperty, GUIContent label)
        {
            SerializedProperty listProperty = serializedProperty.FindPropertyRelative("values");
            GetList(serializedProperty.displayName, listProperty, label);
            return list.GetHeight() + 4;
        }

        private void GetList(string listName, SerializedProperty serializedProperty, GUIContent label)
        {
            if (list == null)
            {
                ReorderableDisplayAttribute attribute = null;
                var attrs = fieldInfo.GetCustomAttributes(true);
                if (attrs.Length > 0)
                {
                    foreach (var attr in attrs)
                    {
                        if (attr is ReorderableDisplayAttribute)
                        {
                            attribute = attr as ReorderableDisplayAttribute;
                            break;
                        }
                    }
                }

                list = new ReorderableList(serializedProperty.serializedObject, serializedProperty, true, true, true, true)
                {
                    drawHeaderCallback = (Rect rect) =>
                        {
                            EditorGUI.LabelField(rect, string.Format("{0}: {1}", listName, serializedProperty.arraySize), EditorStyles.boldLabel);
                        },

                    drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                    {
                        SerializedProperty element = serializedProperty.GetArrayElementAtIndex(index);
                        rect.y += 1.0f;
                        rect.x += 10.0f;
                        rect.width -= 10.0f;

                        if (attribute != null)
                        {
                            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, 0.0f), element, new GUIContent(attribute.header + " " + index), true);
                        }
                        else
                        {
                            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, 0.0f), element, true);
                        }
                    },

                    elementHeightCallback = (int index) =>
                    {
                        return EditorGUI.GetPropertyHeight(serializedProperty.GetArrayElementAtIndex(index)) + 4.0f;
                    }
                };
            }
        }
    }
#endif

    public class CollapsedEventAttribute : PropertyAttribute
    {
        public bool visible;
        public string tooltip;

        public CollapsedEventAttribute(string tooltip = null)
        {
            this.tooltip = tooltip;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(CollapsedEventAttribute))]
    public class CollapsedEventDrawer : UnityEventDrawer
    {
        private AnimBool visible;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // EditorGUI.BeginProperty(position, label, property);
            Init(property);

            EditorGUI.indentLevel++;

            var attr = this.attribute as CollapsedEventAttribute;

            position.height = EditorGUIUtility.singleLineHeight;
            var temp = new GUIContent(label);

            SerializedProperty persistentCalls = property.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (persistentCalls != null)
                temp.text += " (" + persistentCalls.arraySize + ")";

            EditorGUI.BeginChangeCheck();

            if (string.IsNullOrEmpty(temp.tooltip))
            {
                // var tooltipAttribute = fieldInfo.GetCustomAttributes(typeof(TooltipAttribute), true).FirstOrDefault() as TooltipAttribute;
                // var tooltip = tooltipAttribute != null ? tooltipAttribute.tooltip : null;
                temp.tooltip = attr.tooltip;
            }

#if UNITY_2019_1_OR_NEWER
            attr.visible = EditorGUI.BeginFoldoutHeaderGroup(position, attr.visible, temp);
#else
            attr.visible = EditorGUI.Foldout(position, attr.visible, temp, true);
#endif
            if (EditorGUI.EndChangeCheck())
                visible.target = attr.visible;

            position.height = base.GetPropertyHeight(property, label) * visible.faded;
            position.y += EditorGUIUtility.singleLineHeight;
            if (DrawerUtil.BeginFade(visible, ref position))
            {
                var text = label.text;
                label.text = null;
                base.OnGUI(position, property, label);
                label.text = text;
            }
            DrawerUtil.EndFade();
#if UNITY_2019_1_OR_NEWER
            EditorGUI.EndFoldoutHeaderGroup();
#endif
            EditorGUI.indentLevel--;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Init(property);
            return visible.value ? base.GetPropertyHeight(property, label) * visible.faded + EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight;
        }

        private void Init(SerializedProperty property)
        {
            if (visible == null)
            {
                visible = new AnimBool();
                visible.speed = DrawerUtil.AnimSpeed;
                visible.valueChanged.AddListener(() => { DrawerUtil.RepaintInspector(property.serializedObject); });
            }
        }
    }
#endif
    public class CommentAttribute : PropertyAttribute
    {
        public string text;

        public CommentAttribute(string text)
        {
            this.text = text;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(CommentAttribute))]
    public class CommentAttributeDrawer : DecoratorDrawer
    {
        public const string ENABLE_KEY = "ATTRUTILS_ENABLE_HELP_COMMENT";
        private static GUIStyle commentStyle;
        private static Color backgroundColor;
        private static Color rectColor;
        public static bool enableHelpComment;

        public override float GetHeight()
        {
            if (commentStyle == null)
                InitStyle();

            if (!enableHelpComment) return 0;

            var commentAttribute = attribute as CommentAttribute;
            return commentStyle.CalcHeight(new GUIContent(commentAttribute.text), EditorGUIUtility.currentViewWidth) + 8;
        }

        public override void OnGUI(Rect position)
        {
            if (commentStyle == null)
                InitStyle();

            if (!enableHelpComment) return;

            var commentAttribute = attribute as CommentAttribute;
            position.y += 4;
            position.height -= 8;

            EditorGUI.DrawRect(position, backgroundColor);

            position.x += 2;
            position.width -= 2;

            EditorGUI.LabelField(position, commentAttribute.text, commentStyle);
        }

        private void InitStyle()
        {
            commentStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);

            backgroundColor = EditorGUIUtility.isProSkin ? new Color(30 / 255f, 30 / 255f, 30 / 255f) : new Color(1f, 1f, 1f);
            backgroundColor.a = 0.3f;

            rectColor = commentStyle.normal.textColor;
            rectColor.a = 0.5f;

            enableHelpComment = EditorPrefs.GetBool(ENABLE_KEY, true);
        }
    }
#endif

    public class VisibilityAttribute : PropertyAttribute
    {
        public string targetProperty;
        public bool show;
        public bool hideCompletely;

        public bool drawChildrenOnly;
        public bool ignoreVisibility;

        public VisibilityAttribute(string targetProperty, bool show, bool hideCompletely = false)
        {
            this.show = show;
            this.targetProperty = targetProperty;
            this.hideCompletely = hideCompletely;
        }

        public VisibilityAttribute(bool drawChildrenOnly, bool ignoreVisibility)
        {
            this.drawChildrenOnly = drawChildrenOnly;
            this.ignoreVisibility = ignoreVisibility;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(VisibilityAttribute))]
    public class VisibilityAttributeDrawer : PropertyDrawer
    {
        private bool GetPropertyCondition(SerializedProperty property)
        {
            return property != null ? property.boolValue : false;
        }

        private bool GetPropertyCondition(SerializedProperty property, string parentPath, string propName)
        {
            var finalProp = propName.Trim();
            var shouldNegate = finalProp.StartsWith("!");
            if (shouldNegate) finalProp = finalProp.Remove(0, 1);
            var propCondition = GetPropertyCondition(property.serializedObject.FindProperty(parentPath + finalProp));
            if (shouldNegate) propCondition = !propCondition;

            return propCondition;
        }

        private bool GetCondition(SerializedProperty property, VisibilityAttribute visibility)
        {
            string parentPath = null;
            if (property.propertyPath.Contains("."))
            {
                parentPath = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf(".") + 1);
            }

            var result = false;
            if (visibility.targetProperty.Contains("&"))
            {
                result = true;
                var props = visibility.targetProperty.Split('&');
                foreach (var prop in props)
                    result &= GetPropertyCondition(property, parentPath, prop);
            }
            else if (visibility.targetProperty.Contains("|"))
            {
                var props = visibility.targetProperty.Split('|');
                foreach (var prop in props)
                    result |= GetPropertyCondition(property, parentPath, prop);
            }
            else
            {
                result = GetPropertyCondition(property, parentPath, visibility.targetProperty);
            }

            return result;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var visibility = attribute as VisibilityAttribute;
            if (!visibility.ignoreVisibility)
            {
                var condition = GetCondition(property, visibility);

                var dontDraw = !visibility.show;
                if (!condition) dontDraw = !dontDraw;
                if (dontDraw && visibility.hideCompletely) return;

                EditorGUI.BeginDisabledGroup(dontDraw);

                EditorGUI.BeginProperty(position, label, property);
            }

            if (visibility.drawChildrenOnly)
            {
                foreach (var child in property.GetVisibleChildren())
                {
                    position.height = EditorGUI.GetPropertyHeight(child);
                    EditorGUI.PropertyField(position, child);
                    position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                }
            }
            else
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
            EditorGUI.EndProperty();

            if (!visibility.ignoreVisibility)
                EditorGUI.EndDisabledGroup();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var visibility = attribute as VisibilityAttribute;
            if (!visibility.ignoreVisibility)
            {
                var condition = GetCondition(property, visibility);

                var dontDraw = !visibility.show;
                if (!condition) dontDraw = !dontDraw;
                if (dontDraw && visibility.hideCompletely) return 0;
            }

            if (visibility.drawChildrenOnly)
            {
                var height = 0f;
                foreach (var child in property.GetVisibleChildren())
                    height += EditorGUI.GetPropertyHeight(child) + EditorGUIUtility.standardVerticalSpacing;
                return height;
            }
            else
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }
        }
    }
#endif

    public class TitleAttribute : PropertyAttribute
    {
        public string text;
        public Color color;
        public bool spacingTop = true;
        public bool useColor;

        public TitleAttribute(string text) : this(text, 0) { }

        public TitleAttribute(string text, bool spacingTop) : this(text, 0)
        {
            this.spacingTop = spacingTop;
        }

        public static Color HtmlToColor(string colorString)
        {
            if (!colorString.StartsWith("#"))
                colorString = $"#{colorString}";

            ColorUtility.TryParseHtmlString(colorString, out var color);
            return color;
        }

        public TitleAttribute(string text, bool spacingTop, int colorIndex) : this(text, colorIndex)
        {
            this.spacingTop = spacingTop;
        }

        public TitleAttribute(string text, int colorIndex)
        {
            Color[] colors;
            colors = new Color[]
            {
                HtmlToColor("bc87de"),
                HtmlToColor("8ac926"),
                HtmlToColor("ebbc3d"),
                HtmlToColor("e36a68"),
                HtmlToColor("3bceac"),
                HtmlToColor("208ed4"),
            };
            this.text = text;
            this.color = colors[colorIndex];
            useColor = true;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(TitleAttribute))]
    public class TitleAttributeDrawer : DecoratorDrawer
    {
        private static GUIStyle titleStyle;

        private static Color backgroundColor;
        private static Color rectColor;

        public override float GetHeight()
        {
            if (titleStyle == null) Init();

            var titleAttribute = attribute as TitleAttribute;
            if (titleAttribute == null) return base.GetHeight();

            var height = titleStyle.CalcHeight(new GUIContent(titleAttribute.text), EditorGUIUtility.currentViewWidth);
            height += 4;
            if (titleAttribute.spacingTop)
                height += 12;

            return height;
        }

        public override void OnGUI(Rect position)
        {
            if (titleStyle == null) Init();

            var titleAttribute = attribute as TitleAttribute;
            if (titleAttribute == null) return;

            position.height -= 4;
            if (titleAttribute.spacingTop)
            {
                position.y += 12;
                position.height -= 12;
            }

            var rect = new Rect(position);
            rect.width = 2;
            EditorGUI.DrawRect(rect, rectColor);

            var rect2 = new Rect(position);
            rect2.y += rect2.height;
            rect2.height = 1;
            EditorGUI.DrawRect(rect2, new Color(0, 0, 0, 0.15f));

            var accentColor = titleAttribute.color;

            if (!EditorGUIUtility.isProSkin)
            {
                // accentColor = Color.Lerp(accentColor, Color.white, 0.35f);
                accentColor = Color.Lerp(accentColor, new Color(0, 0, 0, 1f), 0.4f);
                // accentColor.a = 0.2f;
            }
            else
            {
                accentColor = Color.Lerp(accentColor, new Color(0, 0, 0, 1f), 0.05f);
            }

            position.x += 2;
            position.width -= 2;
            EditorGUI.DrawRect(position, backgroundColor);

            // if (EditorGUIUtility.isProSkin)
            titleStyle.normal.textColor = accentColor;
            EditorGUI.LabelField(position, titleAttribute.text, titleStyle);
        }

        public static void OnGUILayout(string title)
        {
            if (titleStyle == null) Init();

            GUILayout.Space(8);
            EditorGUILayout.LabelField(title, titleStyle);
        }

        private static void Init()
        {
            titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.normal.textColor = EditorStyles.label.normal.textColor;
            // titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.font = EditorStyles.boldFont;
            titleStyle.stretchWidth = true;
            titleStyle.padding = new RectOffset(6, 4, 4, 4);

            backgroundColor = EditorGUIUtility.isProSkin ? new Color(30 / 255f, 30 / 255f, 30 / 255f) : new Color(1f, 1f, 1f);
            backgroundColor.a = 0.3f;

            rectColor = titleStyle.normal.textColor;
            rectColor.a = 0.5f;
        }
    }
#endif

#if UNITY_EDITOR
    public static class SerializedPropertyUtils
    {
        //https://forum.unity.com/threads/loop-through-serializedproperty-children.435119/#post-5333913

        /// <summary>
        /// Gets visible children of `SerializedProperty` at 1 level depth.
        /// </summary>
        /// <param name="serializedProperty">Parent `SerializedProperty`.</param>
        /// <returns>Collection of `SerializedProperty` children.</returns>
        public static IEnumerable<SerializedProperty> GetVisibleChildren(this SerializedProperty serializedProperty)
        {
            SerializedProperty currentProperty = serializedProperty.Copy();
            SerializedProperty nextSiblingProperty = serializedProperty.Copy();
            {
                nextSiblingProperty.NextVisible(false);
            }

            if (currentProperty.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                        break;

                    yield return currentProperty.Copy();
                }
                while (currentProperty.NextVisible(false));
            }
        }
    }

    public static class DrawerUtil
    {
        public static float AnimSpeed = 10f;
        private static Stack<Color> cacheColors = new Stack<Color>();

        public static bool BeginFade(AnimBool anim, ref Rect rect)
        {
            cacheColors.Push(GUI.color);
            GUI.BeginClip(rect);
            rect.x = 0;
            rect.y = 0;

            if ((double)anim.faded == 0.0)
                return false;
            if ((double)anim.faded == 1.0)
                return true;

            var c = GUI.color;
            c.a = anim.faded;
            GUI.color = c;

            if ((double)anim.faded != 0.0 && (double)anim.faded != 1.0)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    Event.current.Use();
                }

                GUI.FocusControl(null);
            }

            return (double)anim.faded != 0.0;
        }

        public static void EndFade()
        {
            GUI.EndClip();
            GUI.color = cacheColors.Pop();
        }

        public static void RepaintInspector(SerializedObject BaseObject)
        {
            foreach (var item in ActiveEditorTracker.sharedTracker.activeEditors)
                if (item.serializedObject == BaseObject) { item.Repaint(); return; }
        }
    }

#endif

}