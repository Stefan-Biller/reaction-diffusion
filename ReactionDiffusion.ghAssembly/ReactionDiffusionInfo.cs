using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace ReactionDiffusion
{
  public class ReactionDiffusionInfo : GH_AssemblyInfo
  {
    public override string Name => "ReactionDiffusion Info";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon => null;

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "";

    public override Guid Id => new Guid("8fd9348c-fe09-405a-b165-a36ab7ad819f");

    //Return a string identifying you or your company.
    public override string AuthorName => "Stefan Biller";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "stefan-biller.de";

    //Return a string representing the version.  This returns the same version as the assembly.
    public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
  }
}