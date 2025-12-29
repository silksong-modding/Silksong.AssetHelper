# AssetHelper

Helper utilities for loading game assets.

TODO - usage/installation instructions.

Docs TODOs (not on this page):
- Guide on how to find assets you might want to load
- Production cookbook/examples (various ways to use AssetHelper to load assets in prod)
  - Mention that it's good to list the exact game objects you need, not their common ancestor.
  For example, if you need root/child1 and root/child2, let AssetHelper decide whether to
  repack the root or to repack both child1 and child2 without touching the root.
- Common gotchas
  - Never modify a prefab before instantiating it
  - Stuff gets reloaded on quit to menu
  - Construct loadable assets during Awake (at least, dependencies shouldn't be calculated while in-game)
  - Make sure to tell the difference between scenes and sub-scenes
  Also include testing checklist:
  - Make sure loading works in menu, even if you don't need it there, because that's the most likely
    place for things to break
  - Make sure loading works in remote scenes (e.g. mask shard in scenes with no mask shard), because
    that strikes a balance between "likely to break" and "likely to matter", and is easy to verify
	it actually works
  - Make sure loading works in scenes where the object already is, so asset bundle clashes aren't
    happening this is unlikely to matter)
  - Make sure loading works in at least one scene where the object is likely to spawn, assuming that
    there are a limited set of scenes where spawning will happen
	- Obvious example would be a duo boss fight where boss A is added to boss B's scene
  - Make sure to test loading and then changing scene
  - Make sure to test loading and then returning to menu
  - Make sure to test loading, returning to menu and then loading again
