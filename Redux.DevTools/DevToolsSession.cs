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

using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json.Linq;

namespace Redux.DevTools {
    [CreateAssetMenu(fileName = "Redux Dev Tools Session", menuName = "Redux Dev Tools Session")]
    public class DevToolsSession : ScriptableObject {
        public class Step {
            public object Action;
            public Type ActionType;
            public int CollapsedCount;
            public object State;
            public float When;
            public string StackTrace;

            public JToken ActionCache;
            public JToken StateCache;
            public JToken DiffCache;

            public Step() { }
            public Step(in Step prev) {
                Action = prev.Action;
                ActionType = prev.ActionType;
                CollapsedCount = prev.CollapsedCount;
                State = prev.State;
                When = prev.When;
            }
        }
        [HideInInspector] public bool IsRecording;
        [NonSerialized] public List<Step> History = new List<Step>();
        public Dispatcher Dispatch = null;

        public event Action OnClear;
        public event Action<Step> OnAdd;

        public void Clear() {
            History.Clear();
            OnClear();
        }

        private void AddStep(Step step) {
            History.Add(step);
            OnAdd?.Invoke(step);
        }

        public void RecordStep(object action, object state) {
            // Early-out if the action is a thunk
            var actionType = action.GetType();
            if (action is Delegate) {
                return;
            }

            // Remove the first four lines, since those frames are internal to Redux and DevTools
            var stackTrace = Environment.StackTrace;
            var skippedFrameIndex = 0;
            for (int frame = 0; frame < 4; frame++) {
                skippedFrameIndex = stackTrace.IndexOf('\n', skippedFrameIndex + 1);
            }
            stackTrace = stackTrace.Substring(Mathf.Min(skippedFrameIndex + 1, stackTrace.Length));

            var step = new Step {
                Action = action,
                ActionType = actionType,
                State = state,
                When = Time.unscaledTime,
                StackTrace = stackTrace
            };

            if (History.Count > 0) {
                var prevStep = History[History.Count - 1];

                // Action classes marked with the [CollapsibleAction] attribute are aggregated into
                // Lists of actions and are displayed in the devtools as a single row. This is useful for
                // actions that are dispatched very often, like continuous controller input
                var isCollapsible = actionType
                    .GetCustomAttribute<CollapsibleActionAttribute>() != null;
                if (isCollapsible && prevStep.ActionType == actionType) {
                    // If the previous step action is collapsible and of the same type, turn it into a list
                    // and add the current action to it
                    if (prevStep.CollapsedCount == 0) {
                        // TODO: differentiate by stacktrace
                        History[History.Count - 1] = new Step(prevStep) {
                            Action = new List<Step> { prevStep, step },
                            State = state,
                            When = Time.unscaledTime,
                            CollapsedCount = 2
                        };
                    } else {
                        (prevStep.Action as List<Step>).Add(step);
                        prevStep.CollapsedCount++;
                    }

                    return;
                }
            }
            AddStep(step);
        }

        public Middleware<TState> Install<TState>() {
            if (Application.isEditor) {
                return store => next => action => {
                    var dispatched = next(action);
                    if (IsRecording) {
                        RecordStep(action, store.GetState());
                    }
                    return dispatched;
                };
            }
            // Return a pass-through method if we're not in-editor
            return store => next => next;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class CollapsibleActionAttribute : Attribute { }
}