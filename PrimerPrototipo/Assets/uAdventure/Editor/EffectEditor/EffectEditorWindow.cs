﻿using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using uAdventure.Core;

namespace uAdventure.Editor
{
    public class EffectsEditor : CollapsibleGraphEditor<Effects, IEffect>
    {
        private static readonly Vector2 initialSize = new Vector2(200, 50);
        private static GUIContent buttonContent = new GUIContent();
        private readonly Dictionary<IEffect, IEffectEditor> editors = new Dictionary<IEffect, IEffectEditor>();
        private GUIStyle conditionStyle, eitherConditionStyle;
        private readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        public override void Init(Effects content)
        {
            if (conditionStyle == null)
            {
                conditionStyle = new GUIStyle(GUI.skin.box);
                conditionStyle.normal.background = TextureUtil.MakeTex(1, 1, new Color(0.627f, 0.627f, 0.627f));
            }

            if (eitherConditionStyle == null)
            {
                eitherConditionStyle = new GUIStyle(GUI.skin.box);
                eitherConditionStyle.normal.background = TextureUtil.MakeTex(1, 1, new Color(0.568f, 0.568f, 0.568f));
                eitherConditionStyle.padding.left = 15;
            }

            base.Init(content);
        }

        protected override IEffect[] ChildsFor(Effects Content, IEffect parent)
        {
            var randomParent = parent as RandomEffect;
            if (randomParent != null && randomParent.getPositiveEffect() != null)
            {
                if (randomParent.getNegativeEffect() == null)
                {
                    var parentIndex = Content.FindIndex(n => IsPartOf(n, parent));
                    if (parentIndex == Content.Count - 1)
                    {
                        return new IEffect[1] { randomParent.getPositiveEffect() };
                    }
                    else
                    {
                        return new IEffect[2] { randomParent.getPositiveEffect(), Content[parentIndex + 1] };
                    }
                }
                else
                {
                    return new IEffect[2] { randomParent.getPositiveEffect(), randomParent.getNegativeEffect() };
                }
            }
            else
            {
                var index = Content.FindIndex(node => IsPartOf(node, parent));
                return index == Content.Count - 1 ? new IEffect[0] : new IEffect[1] { Content[index + 1] };
            }
        }

        private static bool IsPartOf(IEffect parent, IEffect child)
        {
            if (parent == child)
            {
                return true;
            }

            var randomParent = parent as RandomEffect;
            return randomParent != null && (IsPartOf(randomParent.getPositiveEffect(), child) || IsPartOf(randomParent.getNegativeEffect(), child));
        }

        private static bool IsDirectlyInsideOf(IEffect effectParent, IEffect node)
        {
            var randomParent = effectParent as RandomEffect;
            return randomParent != null && (randomParent.getNegativeEffect() == node || randomParent.getPositiveEffect() == node);
        }

        private IEffect GetParentNode(Effects content, IEffect child)
        {
            var index = content.IndexOf(child);
            if (index == 0)
            {
                return null;
            }
            else if (index > 0)
            {
                var parent = content[index - 1];
                while (parent is RandomEffect && (parent as RandomEffect).getPositiveEffect() != null)
                {
                    parent = (parent as RandomEffect).getPositiveEffect();
                }

                return parent;
            }
            else
            {
                var allNodes = GetNodes(content);
                return allNodes.First(parent => IsDirectlyInsideOf(parent, child));
            }
        }

        protected override void DeleteNode(Effects content, IEffect node)
        {
            var index = content.IndexOf(node);
            if (index == -1)
            {
                var randomEffect = GetParentNode(content, node) as RandomEffect;
                if (randomEffect != null)
                {
                    if (randomEffect.getPositiveEffect() == node)
                    {
                        randomEffect.setPositiveEffect(null);
                        randomEffect.setNegativeEffect(null);
                    }
                    else if (randomEffect.getNegativeEffect() == node)
                    {
                        randomEffect.setNegativeEffect(null);
                    }
                }
            }
            else
            {

                content.Remove(node);
                if (Selection.Contains(node))
                {
                    Selection.Remove(node);
                }
            }
        }

        protected override void DrawOpenNodeContent(Effects content, IEffect node)
        {
            var abstractEffect = node as AbstractEffect;

            if (abstractEffect != null)
            {
                GUILayout.BeginVertical(conditionStyle);
                GUILayout.Label("CONDITIONS");
                if (GUILayout.Button("Add Block"))
                {
                    abstractEffect.getConditions().Add(new FlagCondition(""));
                }

                //##################################################################################
                //############################### CONDITION HANDLING ###############################
                //##################################################################################
                
                var conditions = abstractEffect.getConditions();
                ConditionEditorWindow.LayoutConditionEditor(conditions);

                //##################################################################################

                GUILayout.EndVertical();
            }
            var prevLabelSize = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 75;
            if (editors.ContainsKey(node))
            {
                editors[node].draw();
            }
            EditorGUIUtility.labelWidth = prevLabelSize;
        }

        protected override IEffect[] GetNodes(Effects Content)
        {
            return Content.SelectMany(GetAllNodesIn).ToArray();
        }

        private static IEffect[] GetAllNodesIn(IEffect node)
        {
            var randomNode = node as RandomEffect;
            if (randomNode == null)
            {
                return new []{node};
            }

            var nodes = new List<IEffect> { node };
            if (randomNode.getPositiveEffect() != null)
            {
                nodes.AddRange(GetAllNodesIn(randomNode.getPositiveEffect()));
                if(randomNode.getNegativeEffect() != null)
                {
                    nodes.AddRange(GetAllNodesIn(randomNode.getNegativeEffect()));
                }
            }

            return nodes.ToArray();
        }

        private bool CreateAndInitEffectEditor(Effects content, IEffect node)
        {
            var editor = EffectEditorFactory.Intance.createEffectEditorFor(node);
            if (editor == null)
            {
                Debug.LogWarning("No effect editor available for: " + node.getType().ToString());
                return false;
            }

            if (editor.manages(node))
            {
                editor.Effect = node;
            }
            editor.Window = new Rect(new Vector2(50, 50), initialSize);
            
            var parent = GetParentNode(content, node);
            if (parent != null)
            {
                var pos = editor.Window;
                var randomEffect = parent as RandomEffect;
                if (randomEffect != null && randomEffect != node)
                {
                    var parentPos = editors[parent].Window;
                    pos.position = parentPos.position + new Vector2(parentPos.size.x + 50, 0);

                    if (node == randomEffect.getNegativeEffect())
                    {
                        pos.position = pos.position + new Vector2(0, 250);
                    }
                }
                else
                {
                    var parentPos = editors[parent].Window;
                    pos.position = parentPos.position + new Vector2(parentPos.size.x + 50, 0);
                }

                editor.Window = pos;
            }

            editors[node] = editor;
            return true;
        }

        protected override Rect GetOpenedNodeRect(Effects content, IEffect node)
        {
            if (!editors.ContainsKey(node) && !CreateAndInitEffectEditor(content, node))
            {
                return Rect.zero;
            }

            return editors[node].Window;
        }

        protected override string GetTitle(Effects Content, IEffect node)
        {
            return editors[node].EffectName;
        }

        protected override GUIContent OpenButtonText(Effects content, IEffect node)
        {
            var nodeType = node.getType().ToString();
            if (!icons.ContainsKey(nodeType))
            {
                var n = nodeType.Replace("_", "-").ToLower();
                icons[nodeType] = Resources.Load<Texture2D>("EAdventureData/img/icons/effects/16x16/" + n);
            }
            buttonContent.image = icons[nodeType];
            buttonContent.text = node.ToString();
            return buttonContent;
        }

        protected override void DrawNodeControls(Effects content, IEffect node)
        {
            IEffectEditor editor = null;
            editors.TryGetValue(node, out editor);

            string[] editorNames = EffectEditorFactory.Intance.CurrentEffectEditors;
            int preEditorSelected = EffectEditorFactory.Intance.EffectEditorIndex(node);
            int editorSelected = EditorGUILayout.Popup(preEditorSelected, editorNames);


            if (preEditorSelected != editorSelected)
            {
                editor = EffectEditorFactory.Intance.createEffectEditorFor(editorNames[editorSelected]);
                editor.Window = editors[node].Window;
                editors[editor.Effect] = editor;
                SetCollapsed(content, editor.Effect, IsCollapsed(content, node));

                if (editor.manages(node))
                {
                    editor.Effect = node;
                }
                else
                {
                    // Re insert the conditions
                    var abstractEffect = node as AbstractEffect;
                    if (abstractEffect != null && editor.Effect is AbstractEffect)
                    {
                        (editor.Effect as AbstractEffect).setConditions(abstractEffect.getConditions());
                    }
                    // Replace the effect
                    var index = content.IndexOf(node);
                    if (index == -1)
                    {
                        var randomEffect = GetParentNode(content, node) as RandomEffect;
                        if (randomEffect != null)
                        {
                            if (randomEffect.getPositiveEffect() == node)
                            {
                                randomEffect.setPositiveEffect(editor.Effect);
                            }
                            else if (randomEffect.getNegativeEffect() == node)
                            {
                                randomEffect.setNegativeEffect(editor.Effect);
                            }
                        }
                    }
                    else
                    {
                        content[index] = editor.Effect;
                    }
                }
            }

            base.DrawNodeControls(content, node);
        }

        protected override void SetNodeChild(Effects content, IEffect node, int slot, IEffect child)
        {
            var randomEffect = node as RandomEffect;
            if (randomEffect != null)
            {
                switch (slot)
                {
                    case 0: // Positive effect
                        randomEffect.setPositiveEffect(child);
                        break;
                    case 1: // Negative effect
                        randomEffect.setNegativeEffect(child);
                        break;
                }
            }
            else
            {
                var index = content.IndexOf(node) + 1;
                var childIndex = content.IndexOf(child);
                while (index < childIndex)
                {
                    content.RemoveAt(index);
                    ++index;
                }
            }
        }

        protected override void SetNodePosition(Effects content, IEffect node, Vector2 position)
        {
            if (!editors.ContainsKey(node))
            {
                return;
            }

            var rect = editors[node].Window;
            rect.position = position;
            editors[node].Window = rect;
        }

        protected override void SetNodeSize(Effects content, IEffect node, Vector2 size)
        {
            if (!editors.ContainsKey(node))
            {
                return;
            }

            var rect = editors[node].Window;
            rect.size = size;
            editors[node].Window = rect;
        }

        protected override void MoveNodes(Effects Content, IEnumerable<IEffect> nodes, Vector2 delta)
        {
            foreach(var node in nodes)
            {
                var rect = editors[node].Window;
                rect.position += delta;
                editors[node].Window = rect;
            }
        }
    }

    public class EffectEditorWindow : EditorWindow
    {
        private EffectsEditor effectsEditor;
        private Effects effects;

        public Vector2 scrollPosition = Vector2.zero;
        private GUIContent addButton;
        protected static EffectEditorWindow current;

        public void Init(Effects e)
        {
            var addTex = Resources.Load<Texture2D>("EAdventureData/img/icons/addNode");
            addButton = new GUIContent(addTex);

            EditorWindow.GetWindow<EffectEditorWindow>();
            effects = e;
            effectsEditor = CreateInstance<EffectsEditor>();
            effectsEditor.Repaint = Repaint;
            effectsEditor.BeginWindows = BeginWindows;
            effectsEditor.EndWindows = EndWindows;
            effectsEditor.Init(e);

            EffectEditorFactory.Intance.ResetInstance();

        }

        public void Init(EffectsController e)
        {
            Init(e.getEffectsDirectly());
        }

        protected void OnGUI()
        {
            current = this;

            if (effects == null) {
                Close(); 
				DestroyImmediate (this);
				return;
			}
            this.wantsMouseMove = true;

            var buttonRect = new Rect(position.width - 100, position.height - 100, 80, 80);
            var lastEvent = Event.current.type;
            var eventUsed = false;
            if (buttonRect.Contains(Event.current.mousePosition)
                && Event.current.type != EventType.Repaint
                && Event.current.type != EventType.Layout)
            {
                eventUsed = true;
                Event.current.Use();
            }

            effectsEditor.OnInspectorGUI();

            if (eventUsed)
            {
                Event.current.type = lastEvent; 
            }

            
            if (GUI.Button(buttonRect, addButton))
            {
                CreateEffect((effect) =>
                {
                    effects.Add(effect);
                    Repaint();
                });
            }

            current = null;
        }

        public static void CreateEffect(System.Action<IEffect> action)
        {
            var names = EffectEditorFactory.Intance.CurrentEffectEditors;
            if (names.Length > 0)
            {
                Controller.Instance.ShowInputDialog(TC.get("Effects.SelectEffectType"), TC.get("Effects.SelectEffectType"),
                    EffectEditorFactory.Intance.CurrentEffectEditors, (sender, selected) =>
                    {
                        action(EffectEditorFactory.Intance.createEffectEditorFor(selected).Effect);
                        if (current != null)
                        {
                            current.Repaint();
                        }
                    });
            }
            else
            {
                EditorUtility.DisplayDialog("Cant create", "No effects available!", "Ok");
            }
        }
    }
}