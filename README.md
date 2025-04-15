# Import CAD data as Members in SDS2

This plugin allows you to import any CAD (dwg/dxf) data into SDS2 and convert it to SDS2 Member data. 
Currently lines and polylines are supported.
The program reads a mapping file (can be opened from within the program) that maps either line layer or color to a member type.
The member type and section should be exactly as defined in SDS2 (read the documentation in the mapping file)

 == Architecture ==
 
 THis is an SDS2 plugin with an embedded .Net program.  The .Net program uses the SDS2 .Net API and communicates through a pipe connection with python.

 == Running ==

 Open SDS/2.  You should *not* get warnings when you do, if you do then something is wrong.  Go to Options->Toolbar config.
 Once that opens (it can take a while), click the dropdown and pick Model -- Parametric.  Inside the big list you should
 find a tool called "Import CAD Data as Members".  Drag this onto your toolbar.
 Click OK and save the configuration overtop the old. 
