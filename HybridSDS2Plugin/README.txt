This is an example of how to use the .Net API from an SDS2 command with some interop between the two.  You can
do things like ask SDS2 to run a selection and then send the results back to your .Net program.

 == Architecture ==
 
 This builds an SDS2 plugin with an embedded .Net program.  The .Net program uses the SDS2 .Net API.

 When the SDS2 plugin is run it launches the .Net program, this program creates a named pipe which the .Net
 tool opens and works with.  It then waits for instructions from the .Net program (this way any GUI can be in .Net).

 When the .Net program finishes it tells the python tool to exit and then the .Net program exits.


 == Naming ==

 Everything has "hybrid" in the name.  To use this you should start by renaming things.  Pretty much everything,
 including file names, where you see "hybrid".


 == First thing ==

 Look at Program.cs to get started.  There's a *very* brief example there to work from.

 == Building ==

 Before building you will need to adjust the references to SDS2 libraries.  Under project settings,
 under references.  Remove DesignData.*.  Then add references, add DesignData.SDS2.Linker.dll, DesignData.SDS2.Database.dll,
 and DesignData.SDS2.Model.dll.  Right click Model and Database and go to properties.  Set "Copy Local" to false on each. 
 Leave Linker set to copy local.

 Build for either release or debug, on x64, and then copy all the files dumped into that output 
 directory.  Navigate to your SDS/2 data directory (default is C:\ProgramData\Design Data\$VERSION), 
 and then into a directory called plugins\.  From there, create a new directory, initially HybridSDS2Plugin
 is fine (someday you'll rename that).  Paste your files into that directory.

 So should have this inside your data directory:
 plugins/
 => HybridSDS2Plugin/
    => __init__.py
    => DesignData.SDS2.Linker.dll
    => DesignData.SDS2.Linker.xml (unnecessary but won't hurt)
    => HybridSDS2Plugin.exe
    => HybridSDS2Plugin.exe.config
    => HybridSDS2Plugin.pdb (unnecessary unless debugging)
    => HybridSDS2Plugin.py
    => PLUGIN

 
 == Running ==

 Open SDS/2.  You should *not* get warnings when you do, if you do then something is wrong.  Go to Options->Toolbar config.
 Once that opens (it can take a while), click the dropdown and pick Model -- Parametric.  Inside the big list you should
 find a tool called "Hybrid .Net".  Drag this onto your toolbar, this is the tool made by your plugin (you needed to follow
 Building instructions first).

 Click OK and save the configuration overtop the old.  Now click the "Hybrid .Net button."  A terminal window will pop up, eventually
 it'll print some junk and then SDS/2 will become responsive.  It's now waiting for you to select some members (because the
 default .Net program, in Program.cs, just asks for a selection of members).  Select a few members and right click and select OK.

 After that the terminal should print member numbers and piecemarks for each member you selected.  You can hit enter on the terminal
 to finish the tool.

