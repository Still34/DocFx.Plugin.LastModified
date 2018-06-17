[![Build status](https://ci.appveyor.com/api/projects/status/i58vkd8h4hiy6vhv?svg=true)](https://ci.appveyor.com/project/Still/docfx-plugin-lastmodified)

# DocFx.Plugin.LastModified
A DocFX plugin that adds a last modified date at the end of each conceptual articles.

## Features
* Adds last commit/modified date to each articles
* Shows last commit message at the end of each articles

## Setup
1. Download the binaries from the AppVeyor artifacts linked above (or compile yourself).
2. Create a new folder under your DocFX template folder.
3. Create a new folder called `plugins` under the folder created in the last step.
4. Drop the compiled binaries in.
5. In your `docfx.json` file, modify your template section like so:
```json
    "template": [
      "default",
      "_template/last-modified"
    ],
```
6. Add the post-processor plugin like so:
```json
    "postProcessors": ["LastModifiedPostProcessor"]
```
7. Build the documentation, and it should work now!

# Live Demo
This plugin is currently being used under [Discord.Net](https://github.com/RogueException/Discord.Net) [Documentation](https://docs.stillu.cc/).

# License

This project is licensed under MIT; read `LICENSE` for more details.
