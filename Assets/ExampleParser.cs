using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Pdoxcl2Sharp;

// Use ParseTemplate.tt in VSCode to generate the ParseTemplate.cs, change it's name and store it
// https://github.com/nickbabcock/Pdoxcl2Sharp

/*
    # Hey, I'm a comment, put me anywhere and everything
    # until the end of line won't matter and will be chucked!

    theoretical = {
        infantry_theory
        militia_theory
        mobile_theory
    }
*/

public class TheoreticalFile : IParadoxRead, IParadoxWrite
{
    IList<string> theories = new List<string>();

    // This is the read interface. Whenever the parser finds something
    // interesting it will pass it to this function as the token. From
    // there, we decide if we want to process it further. This function
    // will never receive the whitespace, null, a bracket, etc. With the
    // example, token will be "theoretical", "infantry_theory",
    // "militia_theory", and "mobile_theory"
    public void TokenCallback(ParadoxParser parser, string token)
    {
        // Hey, we know what to do when we see "theoretical"!,
        // we know that it is simply a list of strings.
        if (token == "theoretical")
        {
            theories = parser.ReadStringList();
        }
    }

    // This is the write interface, and unlike the read interface
    // this is not a callback. This simply means that given a writer
    // the class will dump its contents there.
    public void Write(ParadoxStreamWriter writer)
    {
        writer.WriteComment("Hey, I'm a new comment");

        // Write the header for the list. Notice that we include a bracket.
        // The writer will automatically indent subsequent lines by an
        // additional tab
        writer.WriteLine("theoretical = {");
        foreach (var theory in theoryFile.theories)
        {
            writer.WriteLine(theory, ValueWrite.LeadingTabs);
        }
        writer.WriteLine("}");
    }

}

public class Sample
{
    public static void Test()
    {
        TheoreticalFile theoryFile;

        using (FileStream fs = new FileStream("theories.txt", FileMode.Open))
        {
            theoryFile = ParadoxParser.Parse(fs, new TheoreticalFile());
        }
    }
}