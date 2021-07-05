# Redux DevTools for Unity3D
![A demonstration of Redux DevTools with a simple Todo App](https://i.imgur.com/O0MscU1.gif)

A partial reimplementation of [Redux DevTools](https://github.com/reduxjs/redux-devtools), a diagnostic tool for applications that use [Redux](https://redux.js.org/) as a state management solution. This tool works with a slightly modified version of [Redux.NET](https://github.com/GuillaumeSalles/redux.NET), which is included in this package.

## Features
* Recorded history list of all dispatched actions
* Collapsible, JSON-formatted action, state, and diff views for each recorded action
* Stack trace listing for each history step
* Dispatcher that takes JSON text and serializes it to defined action objects

## Installation
You can install this package through Unity's Package Manager via Git URL.

From `Window > Package Manager...`

![Location of the Package Manager menu option](https://i.imgur.com/u3deuTB.png)

Click the '+' symbol in the upper-left, and select "Add package from git URL":

![Adding a Git package from the Unity Package Manager](https://i.imgur.com/6aLDVaH.png)

In the resulting text field, paste the following URL:

```
https://github.com/RolandMQuiros/redux-dev-tools
```

After recompiling, Redux DevTools should be listed in your installed packages:

![The Redux DevTools package successfully installed](https://i.imgur.com/lntINIF.png)

## Usage
Navigate to `Window > Analysis > Redux DevTools` to open the DevTools dialog.

![Where to find the Redux DevTools menu option](https://i.imgur.com/TQE5NCk.png)

If you do not have a `Redux.DevTools.DevToolsSession` asset in your project, opening the dialog from the above menu will create on in your root `Assets/` folder. We'll be using this to attach to your store later.

![The Redux DevTools dialog](https://i.imgur.com/fwDkVKs.png)

Wherever you instantiate your `Store` object, for example in a `MonoBehaviour` or `ScriptableObject`, include `DevToolsSession.Install<TState>()` as one of the additional middleware parameters:

```cs
[SerializeField] private DevToolsSession _session = null;
public IStore<State> Store;

// ...

Store = new Store<State>(
    MainReducer.Reduce,
    new State(),
    _session.Install<State>()
);
```

Afterwards, every call to `Store<TState>.Dispatch` will be recorded in the `DevToolsSession` history.

## Dependencies
* [Redux.NET](https://github.com/GuillaumeSalles/redux.NET)
* [Newtonsoft.JSON](https://www.newtonsoft.com/json)
* [JsonDiffPatchDotNet](https://github.com/wbish/jsondiffpatch.net)
