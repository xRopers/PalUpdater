using System.Windows.Forms;
using System.Drawing;
using System.Linq;

namespace PalUpdater;

public class PreserveFilesForm : Form
{
    private readonly string _browseRoot;
    private readonly List<string> _selectedPaths;
    private TreeView _tree = new();

    private const string PlaceholderTag = "__placeholder__";
    private const string RelPrefix = "ue4ss";

    public List<string>? Result { get; private set; }

    // installPath is Pal\Binaries\Win64 - we browse its "ue4ss" subfolder, since that's where
    // UE4SS now keeps everything worth preserving (mods, settings). Paths in Result stay
    // relative to installPath itself, prefixed with "ue4ss\", since that's what Installer.cs expects.
    public PreserveFilesForm(string installPath, List<string> currentlySelected)
    {
        _browseRoot = Path.Combine(installPath, "ue4ss");
        _selectedPaths = currentlySelected;

        Text = "Choose files to preserve across updates";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        Font = new Font("Segoe UI", 9F);
        Width = 560;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(480, 420);

        BuildUi();
    }

    private void BuildUi()
    {
        const int margin = 15;

        var intro = new Label
        {
            Left = margin, Top = margin, Width = ClientSize.Width - margin * 2, Height = 70,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = "Everything in your UE4SS \"ue4ss\" folder - expand a folder to pick specific items " +
                   "inside it. Checking a folder preserves it and everything in it; unchecked items get " +
                   "overwritten by the update. Mods and UE4SS-settings.ini are checked by default."
        };
        Controls.Add(intro);

        const int buttonRowHeight = 40; // reserved space below the tree for Save/Cancel
        const int treeToButtonGap = 15;

        _tree = new TreeView
        {
            Left = margin, Top = intro.Bottom + 10,
            Width = ClientSize.Width - margin * 2,
            Height = ClientSize.Height - intro.Bottom - 10 - buttonRowHeight - treeToButtonGap,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            CheckBoxes = true
        };
        _tree.BeforeExpand += Tree_BeforeExpand;
        Controls.Add(_tree);

        PopulateTree();

        var okBtn = new Button { Text = "Save", AutoSize = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        Controls.Add(okBtn); // add first so AutoSize resolves before we read Height/Right below
        okBtn.Left = margin;
        okBtn.Top = _tree.Bottom + treeToButtonGap;
        okBtn.Click += (_, _) =>
        {
            Result = CollectChecked();
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelBtn = new Button { Text = "Cancel", AutoSize = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        Controls.Add(cancelBtn);
        cancelBtn.Top = okBtn.Top;
        cancelBtn.Left = okBtn.Right + 10;
        cancelBtn.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
    }

    private void PopulateTree()
    {
        _tree.Nodes.Clear();

        if (!Directory.Exists(_browseRoot))
        {
            _tree.Nodes.Add("(ue4ss folder doesn't exist yet - it'll be created by your next install)");
            _tree.Enabled = false;
            return;
        }

        var entries = Directory.GetFileSystemEntries(_browseRoot).OrderBy(Path.GetFileName).ToList();

        if (entries.Count == 0)
        {
            _tree.Nodes.Add("(ue4ss folder is empty)");
            _tree.Enabled = false;
            return;
        }

        foreach (var fullPath in entries)
            AddNode(_tree.Nodes, fullPath, RelPrefix);
    }

    // relPath is relative to the actual install path (Win64), e.g. "ue4ss\Mods"
    private void AddNode(TreeNodeCollection parentNodes, string fullPath, string parentRelPath)
    {
        var name = Path.GetFileName(fullPath);
        var relPath = Path.Combine(parentRelPath, name);

        var node = new TreeNode(name) { Tag = new NodeInfo(fullPath, relPath) };
        node.Checked = _selectedPaths.Contains(relPath);

        if (Directory.Exists(fullPath) && Directory.EnumerateFileSystemEntries(fullPath).Any())
            node.Nodes.Add(new TreeNode("Loading...") { Tag = PlaceholderTag });

        parentNodes.Add(node);
    }

    private void Tree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        var node = e.Node;
        if (node == null || node.Nodes.Count != 1 || node.Nodes[0].Tag as string != PlaceholderTag)
            return;

        node.Nodes.Clear();
        var info = (NodeInfo)node.Tag!;
        var children = Directory.GetFileSystemEntries(info.FullPath).OrderBy(Path.GetFileName);
        foreach (var childPath in children)
            AddNode(node.Nodes, childPath, info.RelPath);
    }

    private List<string> CollectChecked()
    {
        var result = new List<string>();
        Walk(_tree.Nodes);
        return result;

        void Walk(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is not NodeInfo info) continue;

                if (node.Checked)
                {
                    result.Add(info.RelPath);
                    // parent covers everything under it, no need to also list children
                }
                else if (node.Nodes.Count > 0)
                {
                    Walk(node.Nodes);
                }
            }
        }
    }

    private record NodeInfo(string FullPath, string RelPath);
}
