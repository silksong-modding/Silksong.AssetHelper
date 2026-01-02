# Tips, tricks and common pitfalls


TODO - turn the following to an article, with reference to the most up-to-date code


- Common gotchas
  - Never modify a prefab before instantiating it
  - Stuff gets reloaded on quit to menu
  - Construct loadable assets during Awake (at least, dependencies ideally shouldn't be calculated while in-game)
    - I think this doesn't apply to addressable assets
  - Make sure to tell the difference between scenes and sub-scenes
  - Notes on object paths changing at runtime
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
