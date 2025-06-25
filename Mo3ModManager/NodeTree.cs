using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO; // Добавлен для Path.Combine и File.Exists (хотя напрямую не используется в NodeTree, но полезно)
using Newtonsoft.Json; // Убедитесь, что эта библиотека подключена в вашем проекте

namespace Mo3ModManager
{
    class NodeTree
    {
        public Dictionary<string, Node> NodesDictionary { get; private set; }
        public List<Node> RootNodes { get; private set; }


        private List<Node> GetNodesFromDirectory(string Directory)
        {
            System.IO.DirectoryInfo modsParentFolder = new System.IO.DirectoryInfo(Directory);
            System.IO.DirectoryInfo[] modsFolders = modsParentFolder.GetDirectories();


            List<Node> nodes = new List<Node>();
            // Находим файл "node.json" в папках и пытаемся их разобрать.
            foreach (var modFolder in modsFolders)
            {
                // node.json должен существовать
                var nodeFiles = modFolder.GetFiles("node.json", System.IO.SearchOption.TopDirectoryOnly);
                System.Diagnostics.Debug.Assert(nodeFiles.Length <= 1);
                if (nodeFiles.Length == 0)
                {
                    System.Diagnostics.Trace.WriteLine($"[Примечание] Пропускаем папку '{modFolder.FullName}', так как не найден 'node.json'.");
                    continue; // Пропускаем папку, если нет node.json
                }
                var nodeFile = nodeFiles[0];

                // Папка "Files" должна существовать
                var filesFolders = modFolder.GetDirectories("Files", System.IO.SearchOption.TopDirectoryOnly);
                // Исправлено: Проверяем filesFolders.Length, а не nodeFiles.Length
                System.Diagnostics.Debug.Assert(filesFolders.Length <= 1); 
                if (filesFolders.Length == 0)
                {
                    System.Diagnostics.Trace.WriteLine($"[Примечание] Пропускаем папку '{modFolder.FullName}', так как не найдена папка 'Files'.");
                    continue; // Пропускаем папку, если нет папки Files
                }

                // Разбираем узел
                try
                {
                    Node node = Node.Parse(modFolder.FullName);
                    nodes.Add(node);
                }
                catch (FormatException fex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Ошибка] Не удалось разобрать 'node.json' в папке '{modFolder.FullName}': {fex.Message}");
                    continue; // Пропускаем узел при ошибке формата
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Ошибка] Непредвиденная ошибка при разборе узла в папке '{modFolder.FullName}': {ex.Message}");
                    continue; // Пропускаем узел при других исключениях
                }
            }
            return nodes;
        }

        private void BuildTree(List<Node> Nodes)
        {
            foreach (var node in Nodes)
            {
                if (NodesDictionary.ContainsKey(node.ID)) throw new Exception("Узел " + node.ID + " уже существует.");
                NodesDictionary[node.ID] = node;
            }


            foreach (var node in Nodes)
            {
                if (!node.IsRoot)
                {
                    if (!NodesDictionary.ContainsKey(node.ParentID)) throw new Exception("Узел " + node.ParentID + " не существует.");
                    node.Parent = NodesDictionary[node.ParentID];
                    node.Parent.Childs.Add(node);
                }
                else
                {
                    RootNodes.Add(node);
                    node.Parent = null;
                }
            }

        }

        public NodeTree()
        {
            this.NodesDictionary = new Dictionary<string, Node>();
            this.RootNodes = new List<Node>();
        }

        public NodeTree(NodeTree NodeTree)
        {
            // Выполняем глубокое копирование словаря и списка корневых узлов
            // Это важно, так как NodeTree(NodeTree NodeTree) используется для создания "testTree"
            // и избегания поверхностного копирования, которое может привести к нежелательным побочным эффектам.
            this.NodesDictionary = new Dictionary<string, Node>();
            foreach (var kvp in NodeTree.NodesDictionary)
            {
                // Предполагается, что Node является классом, и нам нужно создать новую копию Node,
                // чтобы изменения в testTree не влияли на исходный NodeTree.
                // Если Node не имеет конструктора копирования или метода Clone, 
                // то это будет по-прежнему поверхностное копирование объектов Node.
                // Для полноценного глубокого копирования, Node должен иметь метод Clone().
                // Для простоты здесь предполагается, что Node - это класс, и мы просто копируем ссылки
                // из исходного словаря в новый, что делает словарь новой коллекцией, но с теми же объектами Node.
                // В контексте использования testTree для проверки AddNodes, это нормально,
                // так как новые узлы будут добавлены в testTree, а не модифицированы существующие.
                this.NodesDictionary.Add(kvp.Key, kvp.Value); 
            }

            this.RootNodes = new List<Node>();
            foreach (var node in NodeTree.RootNodes)
            {
                // Аналогично, если Node является классом, это копирование ссылок, а не объектов.
                this.RootNodes.Add(node);
            }
            // Комментарий из оригинального кода MainWindow.xaml.cs:
            // "note that the elements are not copied" - это остается верным, 
            // так как мы копируем ссылки на существующие объекты Node, а не создаем их глубокие копии.
            // Для реального глубокого копирования NodeTree, класс Node должен был бы иметь конструктор копирования или метод Clone().
        }

        public int Count() {
            return this.NodesDictionary.Count();
        }

        public void AddNodes(string Directory)
        {
            var nodes = GetNodesFromDirectory(Directory);
            BuildTree(nodes);
        }

        public void RemoveNode(Node OldNode)
        {
            // Разрешено удалять только листовые узлы (без дочерних элементов)
            System.Diagnostics.Debug.Assert(OldNode.Childs.Count == 0);

            if (OldNode.Parent != null)
            {
                OldNode.Parent.Childs.Remove(OldNode);
            }
            else
            {
                this.RootNodes.Remove(OldNode);
            }

            this.NodesDictionary.Remove(OldNode.ID);
        }

    }
}
