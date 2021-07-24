# Buff Kit
Guns of Icarus modding toolkit/moderation mod/gond knows what in the future

## Building and installing
Open the solution in an IDE of choice  
Build Spanner  
Copy Spanner.exe to Steam/steamapps/common/Guns of Icarus Online/GunsOfIcarusOnline_Data/Managed/  
Drag-and-drop the Assembly-CSharp.dll onto the Spanner executable  
Back up the original Assembly-CSharp.dll  
Rename Assembly-CSharp_patched.dll to Assembly-CSharp.dll  
Download BepInEx at https://github.com/BepInEx/BepInEx/releases/download/v5.4.13/BepInEx_x86_5.4.13.0.zip  
Extract the BepInEx archive into Steam/steamapps/common/Guns of Icarus Online/  
Rename winhttp.dll to version.dll  
Run the game to make it generate all the BepInEx stuff, then close it  
Open the BuffKit project  
Change the assemblies to the ones in Guns of Icarus Online/GunsOfIcarusOnline_Data/Managed/ and Guns of Icarus Online/BepInEx/core/  
Build BuffKit  
Copy BuffKit.dll to Guns of Icarus Online/BepInEx/plugins/  
Start the game and cry tears of joy

Very simple, as you can see
