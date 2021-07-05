using System;

namespace Redux
{
    public delegate void ActionsCreator<TState>(Dispatcher dispatcher, Func<TState> getState);

    public static partial class Middlewares
    {
        public static Func<Dispatcher, Dispatcher> Thunk<TState>(IStore<TState> store)
        {
            return dispatch => action =>
            {
                var actionsCreator = action as ActionsCreator<TState>;
                if (actionsCreator != null)
                {
                    actionsCreator(store.Dispatch, store.GetState);
                }

                return dispatch(action);
            };
        }
    }
}