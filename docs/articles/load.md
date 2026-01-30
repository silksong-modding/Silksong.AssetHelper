# When to load your assets

## Loading assets

There are many sensible times to load your assets; which one you choose
may depend on how many assets you're loading, how they'll be used, or
just personal preference. Some recommendations might be:

* *In a postfix to GameManager.Awake.* Any earlier than this and there
are likely to be unexpected bugs.

* *When the player enters game.* This can usually be done in a postfix
to GameManager.StartNewGame and a postfix to GameManager.ContinueGame
(both are needed).

* *When the game loads the scene where the asset should be spawned.*
For example, if your asset will only be in use when the player is
in Bonebottom, then it makes sense to load the asset when the game
loads that scene; this is essentially when the base game loads
assets. This behaviour can be achieved in a callback to
`UnityEngine.SceneManagement.SceneManager.sceneLoaded` or `activeSceneChanged`.

* *When the asset will be used.* This will typically be done in a coroutine,
with `yield return managedAsset.Load();` executed before using the asset.

If the function loading the asset is not the one using the asset but the asset
is expected to be loaded , for instance
if the asset is being loaded on enter game, then it may be sensible to use the
@"Silksong.AssetHelper.ManagedAssets.ManagedAssetExtensions.EnsureLoaded``1(Silksong.AssetHelper.ManagedAssets.ManagedAsset{``0})"
method to ensure the asset is loaded before using it.

## Unloading assets

There are several sensible times you may want to unload your assets.

* *When the player leaves the scene where the asset is being used.* Any earlier
than this and any instances of the asset that have been spawned may break.

* *When the player quits to menu.* This should be done in a prefix to QuitToMenu.Start;
it does not matter here that QuitToMenu is a coroutine. This is when
the vanilla game unloads most of the assets that it is using.

* *Never.* It's reasonable to simply not unload the assets at all, although in this case
you should test that things work as intended when you load back into the game.
