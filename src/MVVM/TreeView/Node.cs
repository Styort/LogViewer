using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using LogViewer.Annotations;

namespace LogViewer.MVVM.TreeView
{
    public class Node : INotifyPropertyChanged
    {
        public Node()
        {
            this.Id = Guid.NewGuid().ToString();
        }

        public Node(Node parent, string txt)
        {
            this.Id = Guid.NewGuid().ToString();
            this.Parent = parent;
            this.Text = txt;

            if (Parent != null && parent.Text != "Root")
            {
                this.IsExpanded = Parent.IsExpanded;

                // Формируем путь к классу из предыдущих веток дерева
                Logger = Parent.Logger + "." + Text;
            }
            else
            {
                Logger = Text;
            }
        }


        private string text;
        private bool? isChecked = true;
        private bool isExpanded;
        private bool isSelected = false;

        public ObservableCollection<Node> Children { get; } = new ObservableCollection<Node>();

        public Node Parent { get; }

        public bool? IsChecked
        {
            get => this.isChecked;
            set
            {
                this.isChecked = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Текст чекбокса
        /// </summary>
        public string Text
        {
            get => this.text;
            set
            {
                this.text = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Хранит в себе полный путь к классу
        /// </summary>
        public string Logger { get; set; }

        /// <summary>
        /// Является ли корнем дерева
        /// </summary>
        public bool IsRoot { get; set; }

        /// <summary>
        /// Развернуто ли в дереве
        /// </summary>
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                isExpanded = value;
                OnPropertyChanged();
            }
        }

        private bool isVisible = true;

        /// <summary>
        /// Видимость элемента дерева
        /// </summary>
        public bool IsVisible
        {
            get => isVisible;
            set
            {
                isVisible = value;
                OnPropertyChanged();
            }
        }

        private SolidColorBrush toggleMark = new SolidColorBrush(Colors.Transparent);

        public SolidColorBrush ToggleMark
        {
            get => toggleMark;
            set
            {
                toggleMark = value;
                OnPropertyChanged();
            }
        }

        public string Id { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            if (propertyName == nameof(IsChecked))
            {
                if (this.Id == CheckBoxId.CurrentСheckBoxId && this.Parent == null && this.Children.Count != 0)
                {
                    CheckChildNodes(this.Children, this.IsChecked);
                }
                if (this.Id == CheckBoxId.CurrentСheckBoxId && this.Parent != null && this.Children.Count > 0)
                {
                    CheckChildAndParent(this.Parent, this.Children, this.IsChecked);
                }
                if (this.Id == CheckBoxId.CurrentСheckBoxId && this.Parent != null && this.Children.Count == 0)
                {
                    CheckParentNodes(this.Parent);
                }
            }
        }

        private void CheckChildAndParent(Node parent, ObservableCollection<Node> itemsChild, bool? isChecked)
        {
            CheckChildNodes(itemsChild, isChecked);
            CheckParentNodes(parent);
        }

        private void CheckChildNodes(ObservableCollection<Node> itemsChild, bool? isChecked)
        {
            foreach (Node item in itemsChild)
            {
                item.IsChecked = isChecked;
                if (item.Children.Count != 0) CheckChildNodes(item.Children, isChecked);
            }
        }

        /// <summary>
        /// Для отображения элемента как выбранного
        /// </summary>
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                isSelected = value;
                OnPropertyChanged();
            }
        }

        private void CheckParentNodes(Node parent)
        {
            int countCheck = 0;
            bool isNull = false;

            foreach (Node child in parent.Children)
            {
                if (child.IsChecked == true || child.IsChecked == null)
                {
                    countCheck++;
                    if (child.IsChecked == null)
                        isNull = true;
                }
            }
            if (countCheck != parent.Children.Count && countCheck != 0) parent.IsChecked = null;
            else if (countCheck == 0) parent.IsChecked = false;
            else if (countCheck == parent.Children.Count && isNull) parent.IsChecked = null;
            else if (countCheck == parent.Children.Count && !isNull) parent.IsChecked = true;
            if (parent.Parent != null) CheckParentNodes(parent.Parent);
        }
    }
}
