using System.Threading.Tasks;
using LogViewer.MVVM.ViewModels.Log;
using NUnit.Framework;

namespace LogViewer.App.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class MessageGroupsViewModelTests
    {
        [Test]
        public async Task EmptyBuffer_Open_DoesNotThrow()
        {
            var state = new LogViewState();
            var timer = new FakeUiTimer();
            var vm = new MessageGroupsViewModel(state, new FakeDialogs(), timer);

            vm.OpenCommand.Execute(null);
            await vm.RefreshAsync();

            Assert.That(vm.Items, Is.Empty);
            Assert.That(vm.ShowEmptyPlaceholder, Is.True);
        }

        [Test]
        public void ScheduleRebuild_WhenClosed_DoesNotStartTimer()
        {
            var state = new LogViewState();
            var timer = new FakeUiTimer();
            var vm = new MessageGroupsViewModel(state, new FakeDialogs(), timer);

            vm.ScheduleRebuild();
            Assert.That(timer.IsEnabled, Is.False);
        }

        [Test]
        public void ScheduleRebuild_WhenOpen_CoalescesUntilTick()
        {
            var state = new LogViewState();
            var timer = new FakeUiTimer();
            var vm = new MessageGroupsViewModel(state, new FakeDialogs(), timer);
            vm.OpenCommand.Execute(null);

            vm.ScheduleRebuild();
            vm.ScheduleRebuild();
            Assert.That(timer.IsEnabled, Is.True);
            Assert.That(vm.Items, Is.Empty);
        }
    }
}
