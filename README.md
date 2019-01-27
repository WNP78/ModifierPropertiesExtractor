# ModifierPropertiesExtractor
A script to generate the site for https://wnp78.github.io/Sr2Xml

To use it, get it running in VS (I use VS 2017) and then run it. You should give it the path to your SimpleRockets2 install (the `SimpleRockets2_Data\Managed` folder specifically) as well as the root folder for the site. This should contain a `manifest.xml` file, must have a root element of `<Sr2XmlDocManifest>`. To get the latest of these, clone [this](https://github.com/WNP78/Sr2Xml) repository. Run the `GenPages` command and all pages should be generated, and the `manifest.xml` should be filled in with any new XML options.
