/*
MIT License

Copyright (c) 2020 Roland Quiros

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

using Newtonsoft.Json.Linq;

namespace Redux.DevTools {
    /// <summary>
    /// Displays a JSON object, as parsed by Newtonsoft JSON, in a styled Unity Tree View
    /// </summary>
    public class JSONTreeView : TreeView {
        public JToken Source { get; set; } = null;

        public JSONTreeView(TreeViewState treeViewState) : base(treeViewState) { }

        /// <summary>
        /// Builds the Unity <see cref="TreeView"/> from JSON data
        /// </summary>
        /// <returns>The constructed <see cref="TreeViewItem"/> at the root of the <see cref="TreeView"/></returns>
        protected override TreeViewItem BuildRoot() {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            var allItems = new List<TreeViewItem>();

            if (Source != null) {
                var stack = new Stack<(JToken Token, int Depth)>();
                var idCounter = 1;
                stack.Push((Token: Source, Depth: 0));

                while (stack.Count > 0) {
                    var (token, depth) = stack.Pop();

                    // Skip if parent is a property, since we draw the value in the same row
                    var skip = token.Parent?.Type == JTokenType.Property ||
                        // Skip type property
                        (token.Type == JTokenType.Property && ((JProperty)token).Name == "$type");

                    if (!skip) {
                        allItems.Add(
                            new JSONTreeItem(
                                id: idCounter++,
                                depth: depth++,
                                token: token
                            )
                        );
                    }

                    foreach (var child in token.Children().OrderByDescending(c => c.Path)) {
                        stack.Push((Token: child, Depth: depth));
                    }
                }
            }

            SetupParentsAndChildrenFromDepths(root, allItems);

            return root;
        }

        /// <summary>
        /// Paints an individual row of the <see cref="TreeView"/> to the Editor UI
        /// </summary>
        /// <param name="args">The row data</param>
        protected override void RowGUI(RowGUIArgs args) {
            var item = (JSONTreeItem)args.item;
            var indent = GetContentIndent(item);

            var rect = new Rect(args.rowRect) {
                x = args.rowRect.x + indent
            };

            var _rowStyle = new GUIStyle {
                richText = true,
                normal = new GUIStyleState {
                    textColor = Color.white
                }
            };

            // We want to be able to copy and paste the JSON text. This lets us do so, to some extent.
            var value = TokenValueString(item.Token);
            if (!string.IsNullOrWhiteSpace(value)) {
                EditorGUI.SelectableLabel(rect, args.selected ? $"<b>{value}</b>" : value, _rowStyle);
            }

            // Format and color JSON strings based on token types
            string TokenValueString(JToken token) {
                switch (token.Type) {
                    case JTokenType.Property:
                        var property = (JProperty)token;
                        if (property.Value.Type != JTokenType.Property) {
                            var propValue = TokenValueString(property.Value);
                            if (!string.IsNullOrWhiteSpace(propValue)) {
                                return $"\"{property.Name}\": {propValue}";
                            }
                        }
                        break;
                    case JTokenType.Array:
                        return $"<color=#03f4fc>[</color>{(((JArray)token).Count > 0 ? " ... " : " ")}<color=#03f4fc>]</color>";
                    case JTokenType.Object: {
                        var obj = (JObject)token;
                        var typeProperty = obj.Property("$type");

                        string typeName = " ";
                        if (args.selected && typeProperty != null && typeProperty.Value.Type == JTokenType.String) {
                            typeName = $" {typeProperty.Value} ";
                        } else if (((JObject)token).Properties().Any()) {
                            typeName = " ... ";
                        }

                        return $"<color=#00ffc8>{{</color>{typeName}<color=#00ffc8>}}</color>";
                    }
                    case JTokenType.Float:
                    case JTokenType.Integer:
                        return $"<color=#00ff6e>{token}</color>";
                    case JTokenType.Guid:
                    case JTokenType.Uri:
                    case JTokenType.String:
                        return $"<color=#fcba03>\"{token.ToString()}\"</color>";
                    case JTokenType.Date:
                    case JTokenType.TimeSpan:
                        return $"<color=#429bf5>\"{token.ToString()}\"</color>";
                    case JTokenType.Boolean:
                        return $"<color=#e1ff00>{token}</color>";
                    case JTokenType.Null:
                    case JTokenType.None:
                        return $"<color=#0076de>null</color>";
                    default:
                        return $"<color=#d4cfff>\"{token.ToString()}\"</color>";
                }
                return null;
            }
        }

        public override void OnGUI(Rect rect) {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            base.OnGUI(rect);

            // Reset color
            GUI.backgroundColor = oldColor;
        }
    }

    internal class JSONTreeItem : TreeViewItem {
        public JToken Token { get; private set; }
        public JSONTreeItem(int id, int depth, JToken token) : base(id, depth) {
            Token = token;
        }
    }
}