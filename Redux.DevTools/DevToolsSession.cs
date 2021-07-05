/*
MIT License

Copyright (c) 2021 Roland Quiros

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
    /// <summary>
    /// A storage structure for Redux dispatch history. Holds JSON representations of every action dispatched,
    /// with the resulting state and its diff from the previous state.
    /// </summary>
    /// <remarks>
    /// This structure is a <see cref="ScriptableObject"/> and exists as an asset in the Unity project, so that its data
    /// can persist between Play and Edit mode. This lets developers inspect the dispatch history while the game is
    /// not running.
    /// </remarks>
    [CreateAssetMenu(fileName = "Redux Dev Tools Session", menuName = "Redux Dev Tools Session")]
    public class DevToolsSession : ScriptableObject {
        /// <summary>
        /// A single step in the dispatch history, containing various bits of info about that dispatch, including
        /// stacktraces and timestamps.
        /// </summary>
        public class Step {
            /// <summary>Reference to the dispatched Redux action</summary>
            public object Action;
            /// <summary>The type of the dispatched action, cached so it survives serialization</summary>
            public Type ActionType;
            /// <summary>The number of actions collapsed into this step, for actions dispatched at a high enough
            /// frequency that it affects legibility of the history</summary>
            public int CollapsedCount;
            /// <summary>The resulting state from the dispatch</summary>
            public object State;
            /// <summary>The Unity timestamp, in seconds, when <see cref="this.Action"/> was dispatched</summary>
            public float When;
            /// <summary>The stacktrace of the dispatch, so developers can trace where in code it occured</summary>
            public string StackTrace;
            /// <summary>The JSON-serialized form of <see cref="this.Action"/></summary>
            public JToken ActionCache;
            /// <summary>The JSON-serialized form of <see cref="this.State"/></summary>
            public JToken StateCache;
            /// <summary>The JSON-serialized diff between <see cref="StateCache"/> and the <c>StateCache</c> of the
            /// previous <see cref="Step"/></summary>
            public JToken DiffCache;

            /// <summary>
            /// Constructs a new history <see cref="Step"/>
            /// </summary>
            public Step() { }

            /// <summary>
            /// Constructs a new history <see cref="Step"/> from a different <c>Step</c>.
            /// </summary>
            /// <param name="prev">The other <see cref="Step"/></param>
            public Step(in Step prev) {
                Action = prev.Action;
                ActionType = prev.ActionType;
                CollapsedCount = prev.CollapsedCount;
                State = prev.State;
                When = prev.When;
            }
        }

        /// <summary>
        /// Whether or not the Redux Dev Tools should be recording dispatches for this <see cref="DevToolsSession"/>
        /// </summary>
        [HideInInspector] public bool IsRecording;

        /// <summary>
        /// The list of <see cref="Step"/>s in the dispatch history
        /// </summary>
        [NonSerialized] public List<Step> History = new List<Step>();
        public Dispatcher Dispatch = null;

        /// <summary>Triggered when <see cref="Clear"/> is called.</summary>
        public event Action OnClear;
        /// <summary>Triggered when <see cref="AddStep"/> is called</summary>
        public event Action<Step> OnAdd;

        /// <summary>
        /// Clears the history list and notifies any listeners of <see cref="OnClear"/>
        /// </summary>
        public void Clear() {
            History.Clear();
            OnClear();
        }

        private void AddStep(Step step) {
            History.Add(step);
            OnAdd?.Invoke(step);
        }

        /// <summary>
        /// Adds a dispatched action and its resulting state to the dispatch history, along with additional diagnostic
        /// information.
        /// </summary>
        /// <param name="action">The dispatched action</param>
        /// <param name="state">The resulting state</param>
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

        /// <summary>
        /// Attaches this <see cref="DevToolsSession"/> to a Redux <see cref="IStore{TState}"/> via middleware
        /// installation.
        /// </summary>
        /// <typeparam name="TState">The type of the Redux state object</typeparam>
        /// <returns>
        /// A <see cref="Middleware"/> function that processes each dispatch into the dispatch history
        /// </returns>
        /// <example>
        ///     [SerializeField] private DevToolsSession _devToolsSession;
        ///
        ///     // Create a store with the Redux Dev Tools middleware installed
        ///     var store = new Redux.Store<StateClass>(
        ///         reducer: StateClassReducer.Reduce,
        ///         initialState: new StateClass(),
        ///         _devToolsSession.Install<StateClass>()
        ///     );
        ///
        ///     // All future dispatches are logged in the DevToolsSession
        ///     store.Dispatch(
        ///         new StateAction.AddTodo {
        ///             Description = "First Task",
        ///             Deadline = DateTime.Now + TimeSpan.FromDays(5)
        ///         }
        ///     );
        /// </example>
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

    /// <summary>
    /// Action classes tagged with this attribute will be collapsed in the <see cref="DevToolsWindow"/> display of the
    /// dispatch history if they're dispatched consecutively. Helps with legibility of high-frequency, repeated
    /// actions.
    /// </summary>
    /// <remarks>
    ///     <p>
    ///         The associated state is also collapsed, only containing the resulting state after all collapsed actions
    ///         have been processed.
    ///     </p>
    ///     <p>
    ///         Generally, actions that are dispatched often enough to make the history list unreadable indicate
    ///         behavior that is probably unfit for Redux, due to high memory allocations.
    ///     </p>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class CollapsibleActionAttribute : Attribute { }
}