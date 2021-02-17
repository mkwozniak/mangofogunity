# mangofogunity2d
MangoFog2D is an open source 2D/3D mesh based fog system for the Unity Engine. It's based off TasharenFogOfWar except it uses a complete Mesh based render system with several new features. 
The original blur algorithm and LOS algorithm is all thanks to TasharenFogOfWar. The same technique is applied to the Orthographic2D perspective.
The fog runs on its own thread and doesn't affect performance.
https://github.com/insominx/TasharenFogOfWar

I'm using this for a 2D game I'm developing for fun. Maybe someone else will also find it useful.
Here's a screen shot example scene in the project using some art from opengameart and the Tilemap system.
![MangoFog2D Preview](https://i.imgur.com/o4MgGTB.png)

### Features
* You can easily switch between 2D and 3D perspective modes if you wish.
* Choose between using a MeshRenderer for the fog or just draw directly to the GPU.
* The inspector variables give you easy control over all aspects of the fog.
* Basic example scenes with saving/loading fog buffers included.
* Simple and easy to use, no need for advanced knowledge of Unity.

### Installation

1. Create a GameObject and add the MangoFogInstance component.
2. Take the MangoFogMaterial (in the Materials folder) and drag it into the Fog Material slot.
3. Take the FOWRender shader (in the Shaders folder) and drag it into the Fog Shader slot.
4. Assign the values in the inspector for your desired scene setup. 
5. Create a GameObject and add the MangoFogUnit component.
6. Set the RevealerType to Radius if you want Radius type revealing and LOS if you want Line Of Sight type revealing.
7. Set the radius and/or the Fov Degrees accordingly. (Mind it can produce weird results if you overshoot the degrees and inner/outer radius)
8. That's it! If you set the inspector values correct for your scene, you can press play and see the FoW with your revealer radius.

Here is a standard 2D Orthographic setup in the Inspector.
![MangoFog2D Inspector Setup](https://i.imgur.com/Hez5ZBX.png)

Here is the settings I used in the preview for my 2D character.
![MangoFog2D Revealer 2D Setup](https://i.imgur.com/XKXuHGl.png)

### Not Included Yet
1. Minimap 
* The minimap FoW hasn't yet been implemented. I just haven't found the need for it in my project so I didn't include it. An easy option is to simply render another camera with specific layers. This would achieve a basic minimap

### Experimental Features
The experimental chunks feature allows multiple threads to be created each with their own fog, allowing for the FoW to cover huge areas without rendering massive textures. This isn't fully setup the way I originally intended. Though it works and there is an example scene that tests it out.

### Known Issues
1. When Loading a buffer from the file it can produce flickering in the already explored areas. (Based on the update rate set).
This is only apparent in high quality blurred 3D fog. This is fixed when loading the buffer a second time. Let me know if you have a proper fix

2. In the experimental chunks feature when exiting/entering a chunk there is a visible seam as revealers move through them.
This is apparent in high quality blurred 3D fog and is merely an annoying visual glitch.
If I have time I'll design the multiple chunk system differently to maybe avoid this issue.
If you have any idea what's causing it, let me know, it was annoying me!







