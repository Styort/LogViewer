using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using LogViewer.Annotations;

namespace LogViewer.MVVM.ViewModels
{
    public  class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static PropertyChangedEventArgs CreateArgs<T>(Expression<Func<T, Object>> propertyExpression)
        {
            return new PropertyChangedEventArgs(GetNameFromLambda(propertyExpression));
        }

        private static string GetNameFromLambda<T>(Expression<Func<T, object>> propertyExpression)
        {
            var expr = propertyExpression as LambdaExpression;
            MemberExpression member = expr.Body is UnaryExpression ? ((UnaryExpression)expr.Body).Operand as MemberExpression : expr.Body as MemberExpression;
            var propertyInfo = member.Member as PropertyInfo;
            return propertyInfo.Name;
        }
    }
}
