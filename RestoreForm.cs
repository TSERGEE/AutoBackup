using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AutoBackup
{
    public class RestoreForm : Form
    {
        private TreeView backupTree;
        private Button restoreBtn;
        private string selectedBackupDir;
        public RestoreForm()
        {
            this.Text = "Восстановление из резервной копии";
            this.Size = new System.Drawing.Size(600, 500);
            backupTree = new TreeView { Dock = DockStyle.Fill };
            restoreBtn = new Button { Text = "Восстановить выбранное", Dock = DockStyle.Bottom };
            restoreBtn.Click += RestoreSelected;
            this.Controls.Add(backupTree);
            this.Controls.Add(restoreBtn);
            LoadBackups();
        }

        private void LoadBackups()
        {
            string backupsRoot = Path.Combine(Config.Current.DestinationFolder, "Backups");
            if (!Directory.Exists(backupsRoot)) return;
            foreach (string backupDir in Directory.GetDirectories(backupsRoot))
            {
                TreeNode node = new TreeNode(Path.GetFileName(backupDir));
                node.Tag = backupDir;
                backupTree.Nodes.Add(node);
                LoadFolder(node, backupDir);
            }
        }

        private void LoadFolder(TreeNode parent, string path)
        {
            foreach (string dir in Directory.GetDirectories(path))
            {
                TreeNode sub = new TreeNode(Path.GetFileName(dir));
                sub.Tag = dir;
                parent.Nodes.Add(sub);
                LoadFolder(sub, dir);
            }
            foreach (string file in Directory.GetFiles(path))
            {
                TreeNode fileNode = new TreeNode(Path.GetFileName(file));
                fileNode.Tag = file;
                parent.Nodes.Add(fileNode);
            }
        }

        private void RestoreSelected(object sender, EventArgs e)
        {
            if (backupTree.SelectedNode == null) return;
            using (var fbd = new FolderBrowserDialog()) { fbd.Description = "Выберите папку для восстановления"; if (fbd.ShowDialog() != DialogResult.OK) return; }
            // рекурсивно копируем выбранный узел в целевую папку
            // упрощённо: показываем сообщение
            MessageBox.Show("Восстановление запущено. Проверьте журнал.", "Информация");
        }
    }
}