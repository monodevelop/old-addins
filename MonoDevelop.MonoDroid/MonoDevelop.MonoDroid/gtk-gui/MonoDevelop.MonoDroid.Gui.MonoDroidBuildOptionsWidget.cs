
// This file has been generated by the GUI designer. Do not modify.
namespace MonoDevelop.MonoDroid.Gui {
    public partial class MonoDroidBuildOptionsWidget {
        private global::Gtk.Table table1;
        
        private global::Gtk.Entry extraMonoDroidArgsEntry;
        
        private global::Gtk.Label label1;
        
        protected virtual void Build() {
            global::Stetic.Gui.Initialize(this);
            // Widget MonoDevelop.MonoDroid.Gui.MonoDroidBuildOptionsWidget
            global::Stetic.BinContainer.Attach(this);
            this.Name = "MonoDevelop.MonoDroid.Gui.MonoDroidBuildOptionsWidget";
            // Container child MonoDevelop.MonoDroid.Gui.MonoDroidBuildOptionsWidget.Gtk.Container+ContainerChild
            this.table1 = new global::Gtk.Table(((uint)(2)), ((uint)(2)), false);
            this.table1.Name = "table1";
            this.table1.RowSpacing = ((uint)(6));
            this.table1.ColumnSpacing = ((uint)(6));
            // Container child table1.Gtk.Table+TableChild
            this.extraMonoDroidArgsEntry = new global::Gtk.Entry();
            this.extraMonoDroidArgsEntry.CanFocus = true;
            this.extraMonoDroidArgsEntry.Name = "extraMonoDroidArgsEntry";
            this.extraMonoDroidArgsEntry.IsEditable = true;
            this.extraMonoDroidArgsEntry.InvisibleChar = '●';
            this.table1.Add(this.extraMonoDroidArgsEntry);
            global::Gtk.Table.TableChild w1 = ((global::Gtk.Table.TableChild)(this.table1[this.extraMonoDroidArgsEntry]));
            w1.LeftAttach = ((uint)(1));
            w1.RightAttach = ((uint)(2));
            w1.YOptions = ((global::Gtk.AttachOptions)(4));
            // Container child table1.Gtk.Table+TableChild
            this.label1 = new global::Gtk.Label();
            this.label1.Name = "label1";
            this.label1.LabelProp = global::Mono.Unix.Catalog.GetString("_Extra MonoDroid arguments:");
            this.label1.UseUnderline = true;
            this.table1.Add(this.label1);
            global::Gtk.Table.TableChild w2 = ((global::Gtk.Table.TableChild)(this.table1[this.label1]));
            w2.XOptions = ((global::Gtk.AttachOptions)(4));
            w2.YOptions = ((global::Gtk.AttachOptions)(4));
            this.Add(this.table1);
            if ((this.Child != null)) {
                this.Child.ShowAll();
            }
            this.Hide();
        }
    }
}
