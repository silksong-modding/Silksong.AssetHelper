# Addressables Cheatsheet

In this article we list some common ways to use Addressables in your mod.

## Loading assets

Assets are loaded with a key, which is typically a string; all the keys
used by AssetHelper are strings.

If you have an asset's `key`, you can load it using

```cs
AsyncOperationHandle<T> op = Addressables.LoadAssetAsync<T>(key);
```
Here, `T` is the type of the asset (often this will be GameObject).

## Waiting for the asset to load

When you load the asset, it will not be ready immediately. There are three ways you
can access it:

- Subscribe to the Completed event
You can do this by running
```cs
op.Completed += (handle) =>
{
    // Put your code here
	// handle is effectively the same as op
};
```
This code will be executed when the asset has finished loading. If it has already
finished loading, this code will be executed at the end of the frame.

- Yield return the handle
If you are running a coroutine, you can yield and when control comes back
to you the asset will be loaded. For example
```cs
IEnumerator LoadAssetAndThenDoStuff()
{
    AsyncOperationHandle<GameObject> op = Addressables.LoadAssetAsync<GameObject>(Key);  // Assuming key is defined elsewhere
	yield return op;
	// Do stuff with the op, which has now finished loading
}
```

- Wait until the asset is loaded
You can do this by calling `op.WaitForCompletion();`. This will block the main thread until
the asset has finished loading, so is advised against where possible.

## Accessing the asset

There are two important members of the `AsyncOperationHandle` to be aware of:
- The `op.Result` property, which is the asset you tried to load
- The `op.OperationException` property, which holds any exception if it was thrown when trying to
load the asset.

If the asset is failing to load, logging the Exception can help to figure out why.

## Unloading the asset

To unload the asset, call `Addressables.Release(op);`. This should not be done until the asset
is no longer in use, or things may break - for example, unloading an enemy may cause its
projectiles to fail to exist.
It is not strictly necessary to unload the asset, but may be a good
thing to do if the asset is unlikely to be used.

For convenience, AssetHelper provides the @"Silksong.AssetHelper.Managed.AddressableAsset`1" class
to wrap an addressable asset. This is a single instance that can freely be loaded and unloaded,
without having to construct a new instance.
