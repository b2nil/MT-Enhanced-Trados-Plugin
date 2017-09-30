# MT Enhanced Trados Plugin

A plugin for SDL Trados Studio that allows connecting to Google Translate, Microsoft Translator or Baidu Translate, with additional features like batch find/replace in both directions.

Instructions for building:
<ol>
<li> <b>It is necessary to add a strong name key file</b> to the project, and that key file <b>must</b> be named 'myKey.snk' (or otherwise change the name pointed to in the project properties, under 'Signing')</li>
<li>Also add the necessary SDL references to the SDL_references folder (see references_readme.txt in that folder for more information)</li>
</ol>

The solution and project target 4 versions of SDL Trados Studio (2011, 2014, 2015 and 2017) with one Visual Studio project.  The code is the same for all versions, but the plugin must be built against different library versions and different .NET framework versions.
