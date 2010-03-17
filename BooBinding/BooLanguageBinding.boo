#region license
// Copyright (c) 2005, Peter Johanson (latexer@gentoo.org)
// All rights reserved.
//
// BooBinding is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// BooBinding is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with BooBinding; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
#endregion

namespace BooBinding

import System
import System.IO
import System.Diagnostics
import System.Xml
import System.CodeDom
import System.CodeDom.Compiler;
import Boo.Lang.CodeDom;

import MonoDevelop.Projects
import MonoDevelop.Projects.Dom.Parser
import MonoDevelop.Projects.CodeGeneration
import MonoDevelop.Core

public class BooLanguageBinding(IDotNetLanguageBinding):
	internal static LanguageName = "Boo"
	compilerServices = BooBindingCompilerServices ()
	provider = BooEnhancedCodeProvider ()
//	parser = BooBinding.Parser.BooParser ()
	
	public Language as string:
		get:
			return LanguageName
	
	public ProjectStockIcon as string:
		get:
			return "md-project"
	
	public def CanCompile(fileName as string) as bool:
		Debug.Assert(compilerServices is not null)
		return compilerServices.CanCompile(fileName)
	
	public def Compile (projectItems as ProjectItemCollection , configuration as DotNetProjectConfiguration , configurationSelector as ConfigurationSelector, monitor as IProgressMonitor ) as BuildResult:
		Debug.Assert(compilerServices is not null)
		return compilerServices.Compile (projectItems, configuration, configurationSelector, monitor)
	
	public def CreateCompilationParameters (projectOptions as XmlElement) as ConfigurationParameters:
		parameters = BooCompilerParameters ()
		return parameters
	
	public def CreateProjectParameters (projectOptions as XmlElement) as ProjectParameters:
		return null
	
	public SingleLineCommentTag as string:
		get:
			return "//"
	
	public BlockCommentStartTag as string:
		get:
			return null
	
	public BlockCommentEndTag as string:
		get:
			return null

	def IsSourceCodeFile (fileName as string):
		return Path.GetExtension(fileName).ToLower() == ".boo"

	public def GetCodeDomProvider () as CodeDomProvider:
		return provider
	
	public def GetSupportedClrVersions () as (ClrVersion):
		return array(ClrVersion, (ClrVersion.Net_2_0,))
	
	public def GetFileName (baseName as string) as string:
		return baseName + ".boo"
	
	public Parser as IParser:
		get:
			return null
		
	public Refactorer as IRefactorer:
		get:
			return null

public class BooEnhancedCodeProvider (BooCodeProvider):
	public override def CreateGenerator() as ICodeGenerator:
		return BooEnhancedCodeGenerator ()
		
public class BooEnhancedCodeGenerator (BooCodeGenerator):
	public override def GenerateCompileUnit (cu as CodeCompileUnit):
		// Boo doesn't support more than one namespace in a file.
		// If the compile unit has a default namespace declaration with
		// only imports on it, merge it with the main namespace
		if cu.Namespaces.Count == 2 and cu.Namespaces [0].Name == "":
			for im in cu.Namespaces[0].Imports:
				cu.Namespaces[1].Imports.Add (im)
			cu.Namespaces.RemoveAt (0)
		super.GenerateCompileUnit (cu)
				
