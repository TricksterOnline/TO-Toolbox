# How to compile on Ubuntu

1. Run the `installDeps.sh` script in a terminal.
2. Open `CaballaRE.sln` with MonoDevelop (it's installed now, if it wasn't b4)
3. Switch to "Release" target and select 'Build All' from the 'Build' dropdown

# Alternative compile directions

1. Run the `installDeps.sh` script in a terminal.
2. Go up a directory and run:
```
xbuild /p:Configuration=Release CaballaRE.sln
```

# Running the CaballaRE.exe
1. `CaballaRE.exe` is locate here: `TO-Toolbox/CaballaRE/bin/Release`
2. Open a terminal there and run the program via `mono CaballaRE.exe` not wine.
