# Unity Package Metadata Editor
UPHE is a small tool used to edit the package metadata of a *.unitypackage. With the tool you can add version, name, description or any other supported package header fields. The Unity Asset Manager uses title, description, and version number within its UI to make organizing assets easier.  Once version number, title, and id are set in your package the Asset Manager will start grouping these unitypackages into single entries instead of displaying multiple versions. This is also handy if you use a single file name and rely Dropbox file history for your personal unitypackages.

## Console Version
Usage: uphe (options) --file=/path/to/updated.unitypackage /path/to/file.gz
  Options:
    -p | --print         Print the file comment and quit
    --version=xyz        Set the version of the package to xyz
    --versionid=xyz      Set the version-id of the package to xyz
    --title="Some App"   Sets the title of the package
    --id=xyz             Set the id of the package to xyz
    --pubdate=xyz        Set the version of the package to xyz ex "15 Sept 2018"
    --unityversion=xyz   Set the minimum unity version of the package to xyz
    --categoryid=xyz     Set the category id of the package to xyz
    --category=xyz       Set the version of the package to xyz ex "Editor/Extensions"
    --publisherid=xyz    Set the publisherid of the package to xyz
    --publisher=xyz      Set the version of the package to xyz
    --package.(path).(to).(node)=xyz      Set the value of a custom package node.
                             ex: --package.version=1.1 would match behavior of --version=1.1
    --file=xyz           Set the path where the updated data will be written.
                             NOTE: the file names must not match