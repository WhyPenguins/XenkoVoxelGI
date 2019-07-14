
# XenkoVoxelGI
A voxel cone tracing implementation for the Xenko game engine!  
It's in its early stages but feel free to check it out.  

## Setup  

Prerequisite  
    You'll need to compile the Xenko fork [here](https://github.com/WhyPenguins/xenko), on the branch GIPatches. I had to add a few features to Xenko for the rest of it to work, the changes are really small so check them out if you wish.  

After that's compiled, you can load up the project here and have a look!  
<details>
<summary>If you want to test it out in your own project, it's a little tedious but I'll list everything here: </summary>
<p>

Firstly, add everything in the VoxelGI folder to your project, then...

 1. In the Graphics Compositor:  
	 1. Add a Render Stage, name it Voxel and set its Effect Slot Name to Voxelization  
	 2. Replace the ForwardRenderer with the ForwardRendererVXGI and set all the settings to the previous values  
		 1. Set the Voxel stage to Voxel  
	 3. In MeshRenderFeature  
		1. Add the VoxelPipelineProcessor  
		2. Replace the ForwardLightingRenderFeature with ForwardLightingRenderFeatureVXGI and set all the settings to the previous values  
			1. In addition, add the VoxelLightRenderer to the Light Renderers list  
			2. And set the Reflective Voxel Renderer to ReflectiveVoxelRenderer  
				1. Inside that set the Voxel Stage to Voxel  
		3. Add the VoxelRenderFeature  
			1. Inside that set the Voxelizer Render Stage to Voxel  
		4. Add another MeshTransparentStageSelector  
			1. Set the Effect Name to XenkoForwardShadingEffectVXGI.IsotropicVoxelFragmentEffect  
			2. Set the Opaque Render Stage to Voxel  
			3. Set the Transparent Render Stage to Voxel? If you want...haven't tested it :P  
	4.   In your scene:  
		1. Add a light  
			1. Set its type to Voxel  
			2. Set the bounce intensity to 1  
			3. (At some point you'll have to add a Voxel Volume and link the light to it, but for now the voxel position and resolution are hard-coded until I properly set up the intended clip-map structure)  
I really hope I haven't forgotten anything, let me know if you need any help.

</p>
</details>

## Features  

 Voxelization
- - [X] Geometry shader based axis projection
- - [x] Averaging of fragments lying in the same voxel
- - [x] Solid voxelization (fills areas between front and backfaces with solid black)
- - [x] Downsampling to mip-maps
- - [ ] MSAA voxelization
- - [ ] Clipmaps
- - [ ] Anisotropic Voxels?
- - [ ] Partial revoxelization?

Cone Tracing
- - [x] 12 cone diffuse light (perhaps not done, but working at least)
- - - [ ] More cone setups for different quality
- - [x] Infinite bounces (one frame delay between each one)
- - [ ] Specular
- - [ ] Refraction
- - [ ] Sky light based on sky box?

UI
- - [ ] Debug Voxels
- - [ ] Voxel Volume Component
- - [x] Voxel Light Type

## Notes and Caveats  
Firstly, the volume's resolution and position are hard-coded currently, this'll be fixed when I add clipmaps. Second, it's really unoptimized. Third, I'm getting some really awful flickering when objects move around, am definitely looking into it. And fourth, the shaders take foreeevverrr to generate, so be patient if on first run it constantly freezes when you look around.  

## Implementation Description
### Voxelization  

Just before shadow maps are rendered for the main view, I render them for the voxelization area, then render the scene with lighting.  

Each triangle is projected to the axis with the most visible area via a geometry shader, and then each rasterized fragment is placed into the next available index in a structured buffer. The structured buffer is layed out like a 3D array, with the X and Y spanning the top of the scene, and the Z being vertical columns that the fragments are packed in to.  

Then - in a compute shader - the columns are sorted (currently quite slowly), which allows for some neat tricks. Firstly, it lets me average all the fragments that lie in the same voxel. By summing all the fragments together until we move into a different voxel, and then averaging and storing them, I get no more fragment flickering in a pretty cheap way.  

Then (and this is the main reason) in the same pass, by keeping track of whether the previous fragment was backfacing or not, I can solidify areas between front faces and backfaces. This is pretty robust since there's no requirement for manifold meshes, just pairs of front and back faces (and things like single sided foliage and whatnot should handle well).  

Then the resulting 3D texture is downsampled several times for the different cone tracing radii - so no clip-maps yet. After this rendering proceeds as normal.

### Cone Tracing
 
The voxel lighting is applied using a light, and the same light is used when calculating the initial voxel fragments. This has the effect that you get another bounce each frame effectively for free (which can be quite important for reflections that also have bounce light in them), and I haven't seen any explosions from this so far :P.  And the cone tracing is pretty standard, currently I send out 12 cones which is probably a bit overkill.

<details>
<summary>Info on the classes</summary>
<p>
<b>ForwardLightingRenderFeatureVXGI</b> - Calls ReflectiveVoxelRenderer.Collect just before CollectVisibleLights  
<b>ForwardRendererVXGI</b> - Adds the VoxelStage to the RenderStages and sets up the output validators for it, and calls ReflectiveVoxelRenderer.Draw in DrawCore  


<b>ReflectiveVoxelRenderer</b> - <i>Collect</i>: Creates a render view set for the voxel volume, collects objects for it and adds it to the ShadowMapRenderer.RenderViewsWithShadows. <i>Draw</i>: Creates volume textures and buffers if they don't exist. Calls ShadowMapVolumeRenderer.Draw for the voxel volume view, renders the voxel volume, then calls Arrange Fragments and Generate3DMipmaps  

<b>ArrangeFragments</b> - Compute Shader, each thread sorts a column of voxelFragments and then writes them to the volume texture while averaging ones that lie in the same voxel and filling areas between front and back faces with black  
<b>Generate3DMipmaps</b> - Takes an input texture and downsamples it and outputs it
<b>ClearBuffer</b> - Clears the structured buffer  


<b>VoxelPipelineProcessor</b> - Disables Culling on the Voxel RenderStage

<b>VoxelRenderFeature</b> - Sets up the data needed for storing in the voxel volume for the IsotropicVoxelFragment shader

<b>IsotropicVoxelFragment</b> - Shader that projects to axis of greatest area then stores fragments in append buffer set from VoxelRenderFeature  
<b>IsotropicVoxelFragmentEffect</b> - Effect that uses the shader

<b>XenkoForwardShadingEffectVXGI</b> - Effect that combines lighting and material shaders and whatnot (same as XenkoForwardShadingEffect), has a descendant of IsotropicVoxelFragmentEffect


<b>VoxelVolumeComponent</b> - (TODO)Component with settings for a voxel field


<b>LightVoxel</b> - Component with an intensity, bounceIntensity and (TODO)VoxelVolumeComponent  
<b>LightVoxelEffect</b> - an Effect that uses the LightVoxelShader and adds some variables if they're set  
<b>LightVoxelRenderer</b> - Script that passes data into the LightVoxelShader  
<b>LightVoxelShader</b> - Does a bunch of cone casts against an IComputeVoxelColor, type of IEnvironmentLight  

<b>IComputeVoxelColor</b> - Base class for sampling a position and radius, either RGBA or A only  
<b>IsotropicVoxelColor</b> - Composed of IComputeVoxelColor, samples the correct mipmap/texture from a radius  

</p>
</details>