# MangoFog Unity
MangoFog is a 2D/3D mesh rendered fog of war system for the Unity Engine. It's based off TasharenFogOfWar except it can use a MeshRenderer, SpriteRenderer, or render directly to the GPU. Plus several other features. 
The original blur algorithm and LOS algorithm is all thanks to TasharenFogOfWar. The same technique is applied to the Orthographic2D perspective.
The fog runs on its own thread and doesn't affect performance.
https://github.com/insominx/TasharenFogOfWar

I'm using this for a 2D game I'm developing for fun. Maybe someone else will also find it useful.
Here's a screen shot from the included example scene using some art from opengameart and the Tilemap system.
If there's any comments, questions or issues, email me at mwozniak@wozware.net or mkwozniak@outlook.com.

![MangoFog Preview GIF](https://media.giphy.com/media/KqieE87PNAGAuijVMQ/giphy.gif)

![MangoFog Preview](https://i.imgur.com/o4MgGTB.png)

**This is meant for Unity Versions 2018 +**
**Tested with URP but should work with Built-In**

### Features
* You can easily switch between 2D and 3D perspective modes if you wish.
* Choose between using a MeshRenderer, SpriteRenderer or draw directly to the GPU.
* The inspector variables give you easy control over all aspects of the fog.
* Saving/loading fog included with example scenes.
* Simple and easy to use, no need for advanced knowledge of Unity.
* Fully commented, namespaced and tooltipped. (Almost)

### Installation

1. Create a GameObject and add the MangoFogInstance component.
2. Take the MangoFogMaterial (in the Materials folder) and drag it into the Fog Material slot.
3. Take the FOWRender shader (in the Shaders folder) and drag it into the Fog Shader slot.
4. Assign the values in the inspector for your desired scene setup. 
5. Create a GameObject and add the MangoFogUnit component.
6. Set the RevealerType to Radius if you want Radius type revealing and LOS if you want Line Of Sight type revealing.
7. Set the radius and/or the Fov Degrees accordingly. (Mind it can produce weird results if you overshoot the degrees and inner/outer radius)
8. That's it! If you set the inspector values correct for your scene, you can press play and see the FoW with your revealer radius.

**Make sure to call Dispose() on the MangoFogInstance before switching scenes**

Here is a standard 2D Orthographic setup in the Inspector.

![MangoFog2D Inspector Setup](https://i.imgur.com/Hez5ZBX.png)

Here is the settings I used in the preview for my 2D character.

![MangoFog2D Revealer 2D Setup](https://i.imgur.com/XKXuHGl.png)

### Not Included Yet
1. Minimap 
* The minimap FoW hasn't yet been implemented. I just haven't found the need for it in my project so I didn't include it. The fog can be applied to a Render Texture like any other so it should be easy to implement.

### Experimental Features
The experimental chunks feature allows multiple threads to be created each with their own fog, allowing for the FoW to cover huge areas without rendering massive textures. It works and there is an example scene that tests it out, although
there is a one known issue:
When exiting/entering a chunk there is a visible seam as revealers move through them when using blur.
Any ideas on how to fix this issue would be greatly appreciated.
