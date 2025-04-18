# Import CAD data as Members in SDS2

This plugin allows you to import any CAD (dwg/dxf) data into SDS2 and convert it to SDS2 Member data. 
Currently lines and polylines are supported.
The program reads a mapping file (can be opened from within the program) that maps either line layer or color to a member type.
The member type and section should be exactly as defined in SDS2 (read the documentation in the mapping file).
It is possible to realign workpoints that are misaligned in the CAD data. The algoritm works as follows:
- find coordinates that are alike within tolerance (X,Y,Z)
- check if in same spot some coordinates are already the same
- biggest similarity must be the workpoint
- non-conform coordinates will be moved to this spot

 == Architecture ==
 
 THis is an SDS2 plugin with an embedded .Net program.  The .Net program uses the SDS2 .Net API and communicates through a pipe connection with python.

 == Running ==

 Open SDS/2.  You should *not* get warnings when you do, if you do then something is wrong.  Go to Options->Toolbar config.
 Once that opens (it can take a while), click the dropdown and pick Model -- Parametric.  Inside the big list you should
 find a tool called "Import CAD Data as Members".  Drag this onto your toolbar.
 Click OK and save the configuration overtop the old. 

== Credits ==

Developed by Bert Van Overmeir for Allplan. 

![image](https://github.com/user-attachments/assets/d9ac554a-71b6-4c15-88c3-9441f7a29916)

