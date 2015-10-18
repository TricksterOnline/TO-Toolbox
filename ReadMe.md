# Extracting libconfig data

1. Switch to DAT viewer tab
2. Click on "Open DAT"
3. Choose the libconfig.dat file to decrypt
4. Click on "Export XML"
5. Save the XML file somewhere for later editing

Note: "Export" button will provide the decrypted raw file. However editing on the raw unformatted file is not supported by the editor.

# Editing libconfig

1. Switch to LibConfig Editor tab
2. Click on "Load LibConfig.xml"
3. Choose the XML file that was exported earlier

# Editing tables

1. Select the table you are interested via the listbox
2. To edit fields, double click and type the entry value
3. Click on "Update Table" to synchronize your changes to the editor

Caution: 
* The editor does not check for type safety. If the field expects a number please ensure you enter a number.
* If a string field is empty, ensure that there is at least 1 white-space, otherwise the game client will crash

# Export

1. Click on "Export"
2. Export to "DAT" file. The produced file will be encrypted and (hopefully) accepted by the client and server
3. Export to "IDX" file. 

Caution:
* Ensure that you have click on "Update Table" before exporting, otherwise your changes will not be saved.
* IDX file can only be created after you create the DAT file
* Test if the DAT and IDX files are accepted by the client first (i.e. no crashes) before deploying it to the server

# Localization Helper

The following features are available to speed up localization:
* Export to CSV:
	Export the currently selected table to a CSV file easier editing.
	Recommended to use LibreOffice Calc instead of Microsoft Excel.
* Localization Helper:
	Convenient if you have access to other languages
	It compares values from one table and if they match will perform a copy from one table to the other.
* Import Table:
	Imports a previously exported CSV file

Caution:
* When editing fields that are "blank", ensure that you have at least one whitespace instead of an empty field otherwise the game client will crash
