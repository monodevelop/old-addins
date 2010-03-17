//
// Authors:
//   Ben Motmans  <ben.motmans@gmail.com>
//
// Copyright (C) 2007 Ben Motmans
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using Gtk;
using System;
using MonoDevelop.Core;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.Ide.Gui.Components;

namespace MonoDevelop.Profiling.HeapShot
{
	public class HeapShotProfilingSnapshotNodeBuilder : TypeNodeBuilder
	{
		private EventHandler nameChangedHandler;
		
		public HeapShotProfilingSnapshotNodeBuilder ()
		{
			nameChangedHandler = (EventHandler)DispatchService.GuiDispatch (new EventHandler (OnNameChanged));
		}

		public override Type NodeDataType {
			get { return typeof (HeapShotProfilingSnapshot); }
		}
		
		public override string ContextMenuAddinPath {
			get { return "/MonoDevelop/Profiling/ContextMenu/ProfilingPad/HeapShotProfilingSnapshotNode"; }
		}

		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{
			HeapShotProfilingSnapshot snapshot = (HeapShotProfilingSnapshot)dataObject;
			return snapshot.Name;
		}
		
		public override void GetNodeAttributes (ITreeNavigator treeNavigator, object dataObject, ref NodeAttributes attributes)
		{
			attributes |= NodeAttributes.AllowRename;
		}
		
		public override Type CommandHandlerType {
			get { return typeof (HeapShotProfilingSnapshotNodeCommandHandler); }
		}
		
		public override void BuildNode (ITreeBuilder builder, object dataObject, ref string label, ref Gdk.Pixbuf icon, ref Gdk.Pixbuf closedIcon)
		{
			HeapShotProfilingSnapshot snapshot = (HeapShotProfilingSnapshot)dataObject;
			label = snapshot.Name;
			icon = Context.GetIcon ("md-prof-snapshot");
			snapshot.NameChanged += nameChangedHandler;
		}

		public override bool HasChildNodes (ITreeBuilder builder, object dataObject)
		{
			return false;
		}
		
		protected void OnNameChanged (object sender, EventArgs args)
		{
			ITreeBuilder builder = Context.GetTreeBuilder (sender);
			builder.Update ();
		}
	}
	
	public class HeapShotProfilingSnapshotNodeCommandHandler : NodeCommandHandler
	{
		public override DragOperation CanDragNode ()
		{
			return DragOperation.None;
		}
		
		public override void ActivateItem ()
		{
			HeapShotProfilingSnapshot snapshot = (HeapShotProfilingSnapshot)CurrentNode.DataItem;
			HeapShotProfilingSnapshotView view = new HeapShotProfilingSnapshotView ();
			view.Load (snapshot, false);
			IdeApp.Workbench.OpenDocument (view, true);
		}
		
		public override void RenameItem (string newName)
		{
			IProfilingSnapshot snapshot = (IProfilingSnapshot)CurrentNode.DataItem;
			if (FileService.IsValidFileName (newName))
				snapshot.Name = newName;
			else
				MessageService.ShowError (GettextCatalog.GetString ("Invalid filename"));
		}
		
		public override void DeleteItem ()
		{
			IProfilingSnapshot snapshot = (IProfilingSnapshot)CurrentNode.DataItem;
			ProfilingService.RemoveSnapshot (snapshot);
		}
				
		[CommandHandler (MonoDevelop.Ide.Commands.EditCommands.Rename)]
		public void OnRenameSnapshot ()
		{
			Tree.StartLabelEdit ();
		}
	}
}