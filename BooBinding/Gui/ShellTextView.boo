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

namespace BooBinding.Gui

import System
import System.Collections
import System.IO

import Gtk
import Gdk

import MonoDevelop.Components
import MonoDevelop.Ide.CodeCompletion
import MonoDevelop.Core
import MonoDevelop.Projects
import MonoDevelop.Ide
import MonoDevelop.Projects.Dom.Parser

/*
 * TODO
 * 
 * 1) Don't record lines with errors in the _scriptLines buffer
 */

class ShellTextView (TextView, ICompletionWidget):
	private static _promptRegular = ">>> "
	private static _promptMultiline = "... "
	
	[Getter(Model)]
	model as IShellModel

	private _scriptLines = ""
	
	private _commandHistoryPast as Stack = Stack()
	private _commandHistoryFuture as Stack = Stack()
	
	private _inBlock as bool = false
	private _blockText = ""

	private _reset_clears_history as bool
	private _reset_clears_scrollback as bool
	private _auto_indent as bool
	private _load_assembly_after_build as bool

	private _proj as Project

	private _assembliesLoaded as bool

	private _fakeProject as DotNetProject
	private _fakeSolution as Solution
	private _fakeFileName as string
	private _fileInfo as FileStream
	private _parserContext as ProjectDom;
	
	def constructor(model as IShellModel):
		
		self.model = model
		self.WrapMode = Gtk.WrapMode.Word
		self.ModifyFont(Model.Properties.Font)

		# FIXME: Put the project file somewhere other than /tmp
		shellProjectFile = System.IO.Path.Combine (MonoDevelop.Core.PropertyService.Locations.Cache, "${Model.LanguageName}-shell-project.mdp")

		// 'touch' the file so the MD parsing foo sees it as existing.
		_fakeFileName = System.IO.Path.Combine (MonoDevelop.Core.PropertyService.Locations.Cache, "shell-dummy-file.${Model.MimeTypeExtension}")
		if not System.IO.File.Exists (_fakeFileName):
			_fileInfo  = System.IO.File.Create (_fakeFileName)
			_fileInfo.Close ()
		_fakeProject = DotNetAssemblyProject(Model.LanguageName, Name: "___ShellProject", FileName: shellProjectFile)
		_fakeSolution = Solution()
		_fakeSolution.RootFolder.AddItem(_fakeProject)
		ProjectDomService.Load (_fakeSolution)
		
		_parserContext = ProjectDomService.GetProjectDom (_fakeProject)

		Model.Properties.InternalProperties.PropertyChanged += OnPropertyChanged
		Model.RegisterOutputHandler (HandleOutput)

		_auto_indent = Model.Properties.AutoIndentBlocks
		_reset_clears_scrollback = Model.Properties.ResetClearsScrollback
		_reset_clears_history = Model.Properties.ResetClearsHistory
		_load_assembly_after_build = Model.Properties.LoadAssemblyAfterBuild


		// The 'Freezer' tag is used to keep everything except
		// the input line from being editable
		tag = TextTag ("Freezer")
		tag.Editable = false
		Buffer.TagTable.Add (tag)
		prompt (false)

		IdeApp.ProjectOperations.EndBuild += ProjectCompiled
		IdeApp.ProjectOperations.CurrentProjectChanged += ProjectChanged

		// Run our model. Needs to happen for models which may spawn threads,
		// processes, etc
		Model.Run()
	
	def ProjectChanged (sender, e as ProjectEventArgs):
		_proj = e.Project

	def ProjectCompiled (sender, args as BuildEventArgs):
		if _load_assembly_after_build and args.Success:
			Model.Reset()
			resetGui()
			loadProjectAssemblies ()

	def loadProjectAssemblies():
		for assembly in getProjectAssemblies ():
			if (System.IO.File.Exists(assembly)):
				Model.Reset()
				Model.LoadAssembly (assembly)
		_assembliesLoaded = true
					

	def getProjectAssemblies():
		_assemblies = []
		if (_proj is not null):
			assembly = _proj.GetOutputFileName(ConfigurationSelector.Default)
			if not assembly.IsNull:
				_assemblies.Add(assembly)
		else:
			projects = IdeApp.Workspace.GetAllProjects()
			for entry as Project in projects:
				if entry is null:
					continue
				assembly = entry.GetOutputFileName(ConfigurationSelector.Default)
				if not assembly.IsNull:
					_assemblies.Add(assembly)

		return _assemblies

	def HandleOutput():
		GLib.Idle.Add (outputIdleProcessor)
	
	def outputIdleProcessor() as bool:
		output = Model.GetOutput()
		if output is not null:
			for line as string in output:
				processOutput (line )
				
		prompt (true)
		for assembly in Model.References:
			_fakeProject.AddReference(assembly)

		GLib.Idle.Add () do:
			ProjectDomService.Parse (_fakeProject, _fakeFileName, _scriptLines)
		return false
			
	override def Dispose():
		Model.Dispose()

	#region Overrides of the standard methods for event handling
	override def OnPopulatePopup (menu as Gtk.Menu):
		_copyScriptInput = ImageMenuItem (GettextCatalog.GetString ("Copy Script"))
		_copyScriptInput.Activated += { Gtk.Clipboard.Get (Gdk.Atom.Intern ("PRIMARY", true)).Text = _scriptLines }
		_copyScriptInput.Image = Gtk.Image (Stock.Copy, Gtk.IconSize.Menu)
		
		_saveScriptToFile = ImageMenuItem (GettextCatalog.GetString ("Save Script As ..."))
		_saveScriptToFile.Image = Gtk.Image (Stock.SaveAs, Gtk.IconSize.Menu)
		_saveScriptToFile.Activated += OnSaveScript
		
		_loadAssemblies = ImageMenuItem (GettextCatalog.GetString ("Load Project Assemblies (forces shell reset)"))
		_loadAssemblies.Image = Gtk.Image (Stock.Add, Gtk.IconSize.Menu)
		_loadAssemblies.Activated += def():
			if Model.Reset ():
				resetGui ()
				loadProjectAssemblies ()
		
		_reset = ImageMenuItem (GettextCatalog.GetString ("Reset Shell"))
		_reset.Image = Gtk.Image (Stock.Clear, Gtk.IconSize.Menu)
		_reset.Activated += def():
			if Model.Reset():
				resetGui()
				_assembliesLoaded = false

		if _scriptLines.Length <= 0:
			_copyScriptInput.Sensitive = false
			_saveScriptToFile.Sensitive = false
			_reset.Sensitive = false

		if (_assembliesLoaded == false) and (len (getProjectAssemblies ()) > 0):
			_loadAssemblies.Sensitive = true
		else:
			_loadAssemblies.Sensitive = false

		_sep = Gtk.SeparatorMenuItem()
		menu.Prepend(_sep)
		menu.Prepend(_copyScriptInput)
		menu.Prepend(_saveScriptToFile)
		menu.Prepend(_loadAssemblies)
		menu.Prepend(_reset)
		
		_sep.Show()
		_copyScriptInput.Show()
		_saveScriptToFile.Show()
		_loadAssemblies.Show()
		_reset.Show()
	
	override def OnKeyPressEvent (ev as Gdk.EventKey):
		ka as KeyActions
		processkeyresult as bool
		// TODO: cast ((char), ev.Key) (seems not to work)
		processkeyresult = CompletionWindowManager.PreProcessKeyEvent (ev.Key, char('\0'), ev.State, ka)
		CompletionWindowManager.PostProcessKeyEvent (ka)
		if processkeyresult:
			return true
		
		// Short circuit to avoid getting moved back to the input line
		// when paging up and down in the shell output
		if ev.Key in (Gdk.Key.Page_Up, Gdk.Key.Page_Down):
			return super (ev)
		
		// Needed so people can copy and paste, but always end up
		// typing in the prompt.
		if Cursor.Compare (InputLineBegin) < 0:
			Buffer.MoveMark (Buffer.SelectionBound, InputLineEnd)
			Buffer.MoveMark (Buffer.InsertMark, InputLineEnd)
		
		if (ev.State == Gdk.ModifierType.ControlMask) and ev.Key == Gdk.Key.space:
			TriggerCodeCompletion ()

		if ev.Key == Gdk.Key.Return:
			if _inBlock:
				if InputLine == "":
					processInput (_blockText)
					_blockText = ""
					_inBlock = false
				else:
					_blockText += "\n${InputLine}"
					if _auto_indent:
						_whiteSpace = /^(\s+).*/.Replace(InputLine, "$1")
						if InputLine.Trim()[-1:] == ":":
							_whiteSpace += "\t"
					prompt (true, true)
					if _auto_indent:
						InputLine += "${_whiteSpace}"
			else:
				// Special case for start of new code block
				if InputLine.Trim()[-1:] == ":":
					_inBlock = true
					_blockText = InputLine
					prompt (true, true)
					if _auto_indent:
						InputLine += "\t"
					return true

				// Bookkeeping
				if InputLine != "":
					// Everything but the last item (which was input),
					//in the future stack needs to get put back into the
					// past stack
					while _commandHistoryFuture.Count > 1:
						_commandHistoryPast.Push(cast(string,_commandHistoryFuture.Pop()))
					// Clear the pesky junk input line
					_commandHistoryFuture.Clear()

					// Record our input line
					_commandHistoryPast.Push(InputLine)
					if _scriptLines == "":
						_scriptLines += "${InputLine}"
					else:
						_scriptLines += "\n${InputLine}"
				
					processInput (InputLine)
			return true

		// The next two cases handle command history	
		elif ev.Key == Gdk.Key.Up:
			if (not _inBlock) and _commandHistoryPast.Count > 0:
				if _commandHistoryFuture.Count == 0:
					_commandHistoryFuture.Push(InputLine)
				else:
					if _commandHistoryPast.Count == 1:
						return true
					_commandHistoryFuture.Push(cast(string,_commandHistoryPast.Pop()))
				InputLine = cast (string, _commandHistoryPast.Peek())
			return true
			
		elif ev.Key == Gdk.Key.Down:
			if (not _inBlock) and _commandHistoryFuture.Count > 0:
				if _commandHistoryFuture.Count == 1:
					InputLine = cast(string, _commandHistoryFuture.Pop())
				else:
					_commandHistoryPast.Push (cast(string,_commandHistoryFuture.Pop()))
					InputLine = cast (string, _commandHistoryPast.Peek())
			return true
			
		elif ev.Key == Gdk.Key.Left:
			// Keep our cursor inside the prompt area
			if Cursor.Compare (InputLineBegin) <= 0:
				return true

		elif ev.Key == Gdk.Key.Home:
			Buffer.MoveMark (Buffer.InsertMark, InputLineBegin)
			// Move the selection mark too, if shift isn't held
			if (ev.State & Gdk.ModifierType.ShiftMask) == ev.State:
				Buffer.MoveMark (Buffer.SelectionBound, InputLineBegin)
			return true

		elif ev.Key == Gdk.Key.period:
			ret = super.OnKeyPressEvent(ev)
//			prepareCompletionDetails (Buffer.GetIterAtMark (Buffer.InsertMark))
//			CompletionListWindow.ShowWindow(char('.'), CodeCompletionDataProvider (_parserContext, _ambience, _fakeFileName, true), self)
			return ret

		// Short circuit to avoid getting moved back to the input line
		// when paging up and down in the shell output
		elif ev.Key in (Gdk.Key.Page_Up, Gdk.Key.Page_Down):
			return super (ev)
		
		return super (ev)
	
	protected override def OnFocusOutEvent (e as EventFocus):
		CompletionWindowManager.HideWindow ()
		return super.OnFocusOutEvent(e)
	
	#endregion

	private def TriggerCodeCompletion():
		iter = Cursor
		triggerChar = char('\0')
		triggerIter = TextIter.Zero
		if (iter.Char != null and  iter.Char.Length > 0):
			if iter.Char[0] in (char(' '), char('\t'), char('.'), char('('), char('[')):
				triggerIter = iter
				triggerChar = iter.Char[0]

		while iter.LineOffset > 0 and triggerIter.Equals (TextIter.Zero):
			if (iter.Char == null or iter.Char.Length == 0):
				iter.BackwardChar ()
				continue

			if iter.Char[0] in (char(' '), char('\t'), char('.'), char('('), char('[')):
				triggerIter = iter
				triggerChar = iter.Char[0]
				break

			iter.BackwardChar ()
		
		if (triggerIter.Equals (TextIter.Zero)):
			return

		triggerIter.ForwardChar ()
		
//		prepareCompletionDetails (triggerIter)
//		CompletionListWindow.ShowWindow (triggerChar, CodeCompletionDataProvider (_parserContext, _ambience, _fakeFileName, true), self)

	// Mark to find the beginning of our next input line
	private _endOfLastProcessing as TextMark

	#region Public getters for useful values
	public InputLineBegin as TextIter:
		get:
			endIter = Buffer.GetIterAtMark (_endOfLastProcessing)
			return endIter
	
	public InputLineEnd as TextIter:
		get:
			return Buffer.EndIter
	
	private Cursor as TextIter:
		get:
			return Buffer.GetIterAtMark (Buffer.InsertMark)
	#endregion
	
	// The current input line
	public InputLine as string:
		get:
			return Buffer.GetText (InputLineBegin, InputLineEnd, false)
		set:
			start = InputLineBegin
			end = InputLineEnd
			Buffer.Delete (start, end)
			start = InputLineBegin
			Buffer.Insert (start, value)
	
	#region local private methods
	private def processInput (line as string):
		Model.QueueInput (line)
	
	private def processOutput (line as string):
		end = Buffer.EndIter
		Buffer.Insert (end , "\n${line}")

	private def prompt (newLine as bool):
		prompt (newLine, false)

	private def prompt (newLine as bool, multiline as bool):
		if newLine:
			Buffer.Insert (Buffer.EndIter , "\n")
		if multiline:
			Buffer.Insert (Buffer.EndIter , "${_promptMultiline}")
		else:
			Buffer.Insert (Buffer.EndIter , "${_promptRegular}")

		Buffer.PlaceCursor (Buffer.EndIter)
		ScrollMarkOnscreen(Buffer.InsertMark)
		

		// Record the end of where we processed, used to calculate start
		// of next input line
		_endOfLastProcessing = Buffer.CreateMark (null, Buffer.EndIter, true)

		// Freeze all the text except our input line
		Buffer.ApplyTag(Buffer.TagTable.Lookup("Freezer"), Buffer.StartIter, InputLineBegin)
		
	private def resetGui():
		if _reset_clears_scrollback:
			Buffer.Text = ""
		if _reset_clears_history:
			_commandHistoryFuture.Clear()
			_commandHistoryPast.Clear()

		_scriptLines = ""
		prompt(not _reset_clears_scrollback)
		
	// FIXME: Make my FileChooser use suck less
	private def OnSaveScript():
		_sel = SelectFileDialog("Save Script ...", FileChooserAction.Save)
		if _sel.Run():
			_path = _sel.SelectedFile
			using writer = StreamWriter (_path):
				writer.Write (_scriptLines)
	
	def OnPropertyChanged (obj as object, e as PropertyChangedEventArgs):
		if e.Key == "Font":
			self.ModifyFont(Model.Properties.Font)
		elif e.Key == "AutoIndentBlocks":
			_auto_indent = Model.Properties.AutoIndentBlocks
		elif e.Key == "ResetClearsScrollback":
			_reset_clears_scrollback = Model.Properties.ResetClearsScrollback
		elif e.Key == "ResetClearsHistory":
			_reset_clears_history = Model.Properties.ResetClearsHistory
		elif e.Key == "LoadAssemblyAfterBuild":
			_load_assembly_after_build = Model.Properties.LoadAssemblyAfterBuild

		return

	#endregion

	#region ICompletionWidget
	
	public event CompletionContextChanged as EventHandler;

	def ICompletionWidget.CreateCodeCompletionContext (triggerOffset as int) as CodeCompletionContext:
		triggerIter = Buffer.GetIterAtOffset (triggerOffset);
		rect = GetIterLocation (Buffer.GetIterAtMark (Buffer.InsertMark))

		wx as int
		wy as int
		BufferToWindowCoords (Gtk.TextWindowType.Widget, rect.X, rect.Y + rect.Height, wx, wy)

		tx as int
		ty as int
		GdkWindow.GetOrigin (tx, ty)

		ctx = CodeCompletionContext ();
		ctx.TriggerOffset = triggerIter.Offset;
		ctx.TriggerLine = triggerIter.Line;
		ctx.TriggerLineOffset = triggerIter.LineOffset;
		ctx.TriggerXCoord = tx + wx;
		ctx.TriggerYCoord = ty + wy;
		ctx.TriggerTextHeight = rect.Height;
		return ctx;
		
	
	ICompletionWidget.SelectedLength:
		get:
			select1 as TextIter
			select2 as TextIter
			if Buffer.GetSelectionBounds (select1, select2):
				return Buffer.GetText (select1, select2, true).Length
			else:
				return 0
	
	ICompletionWidget.TextLength:
		get:
			return Buffer.EndIter.Offset
	
	def ICompletionWidget.GetChar (offset as int) as System.Char:
		return Buffer.GetIterAtLine (offset).Char[0]
	
	def ICompletionWidget.GetText (startOffset as int, endOffset as int) as string:
		return Buffer.GetText(Buffer.GetIterAtOffset (startOffset), Buffer.GetIterAtOffset(endOffset), true)
	
	def ICompletionWidget.GetCompletionText (ctx as CodeCompletionContext) as string:
		return Buffer.GetText (Buffer.GetIterAtOffset (ctx.TriggerOffset), Buffer.GetIterAtMark (Buffer.InsertMark), false);
	
	def ICompletionWidget.SetCompletionText (ctx as CodeCompletionContext, partial_word as string, complete_word as string):
		offsetIter = Buffer.GetIterAtOffset(ctx.TriggerOffset)
		endIter = Buffer.GetIterAtOffset (offsetIter.Offset + partial_word.Length)
		Buffer.MoveMark (Buffer.InsertMark, offsetIter)
		Buffer.Delete (offsetIter, endIter)
		Buffer.InsertAtCursor (complete_word)
		
	def ICompletionWidget.Replace (offset as int, count as int, text as string):
		offsetIter = Buffer.GetIterAtOffset(offset)
		endIter = Buffer.GetIterAtOffset (offsetIter.Offset + count)
		Buffer.MoveMark (Buffer.InsertMark, offsetIter)
		Buffer.Delete (offsetIter, endIter)
		Buffer.InsertAtCursor (text)
	
	ICompletionWidget.GtkStyle:
		get:
			return self.Style.Copy();
	#endregion
