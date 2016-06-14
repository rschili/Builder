Builder
=======

Introduction
------------

I mostly wrote this for myself both as a programming practice and as a tool that I will use myself on a daily basis.
For people not working for my company, this tool will be pretty pointless, but you can still have a look at the code.

![Picture of Main Window](Builder/doc/main.png)

Get and Build instructions
--------------------------

To run the application, you can just download the latest binary from ["Releases"](https://github.com/rschili/Builder/releases)

To get and build you need the Builder repository and also the RSCoreLib repository (hyperlink below, it's on the same github account).
Put the two repositories alongside so they will share the same parent directory.

Open up the Builder solution file in Visual Studio 2015, restore the referenced nuget packages, and you should be fine to to debug builds.
Currently, Release builds require a key to sign the assemblies which I did not include in the repository.

Dependencies
------------

-   [RSCoreLib](https://github.com/rschili/RSCoreLib) Personal Core Library of mine for utility features

External Dependencies
---------------------

-   [Hardcodet.Wpf.TaskbarNotification](https://bitbucket.org/hardcodet/notifyicon-wpf/) (CPOL)
-   [log4net](https://logging.apache.org/log4net/) (Apache License)
-   [mah metro](https://github.com/MahApps/MahApps.Metro) (Ms-PL)
-   [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) (MIT License)
-   [System.Data.Sqlite](https://system.data.sqlite.org/) (Ms-PL)
-   [System.Threading.Tasks.Dataflow](https://www.nuget.org/packages/System.Threading.Tasks.Dataflow) (.NET Library License)
-   [Fatcow Icon Library](http://www.fatcow.com/free-icons) (Creative Commons Attribution 3.0 License)
-   [Visual Studio Image Library](https://www.microsoft.com/en-us/download/details.aspx?id=35825) (MSLT)

For all additional information, please refer to the user readme file
--------------------------------------------------------------------

[Readme](Builder/doc/Readme.md)
