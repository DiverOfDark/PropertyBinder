using System;
using NUnit.Framework;
using Shouldly;

namespace PropertyBinder.Tests
{
    [TestFixture]
    internal class CommandBindingsFixture : BindingsFixture
    {
        [Test]
        public void ShouldBindCommand()
        {
            int canExecuteCalls = 0;
            _binder.BindCommand(x => x.Int++, x => x.Flag && x.String != null).To(x => x.Command);

            using (_binder.Attach(_stub))
            {
                _stub.Command.ShouldNotBeNull();
                _stub.Command.CanExecute(null).ShouldBe(false);

                _stub.Command.CanExecuteChanged += (s, e) => { ++canExecuteCalls; };
                _stub.Flag = true;
                canExecuteCalls.ShouldBe(0);
                _stub.Command.CanExecute(null).ShouldBe(false);
                _stub.String = "a";
                canExecuteCalls.ShouldBe(1);
                _stub.Command.CanExecute(null).ShouldBe(true);

                _stub.Int.ShouldBe(0);
                _stub.Command.Execute(null);
                _stub.Int.ShouldBe(1);
            }
        }

        [Test]
        public void ShouldBindCommandWithParameter()
        {
            int canExecuteCalls = 0;
            _binder.BindCommand((x, par) => x.Int += (int)par, (x, par) => x.Flag && (int)par > 0).To(x => x.Command);

            using (_binder.Attach(_stub))
            {
                _stub.Command.ShouldNotBeNull();
                _stub.Command.CanExecute(0).ShouldBe(false);
                _stub.Command.CanExecute(2).ShouldBe(false);

                _stub.Command.CanExecuteChanged += (s, e) => { ++canExecuteCalls; };
                _stub.Flag = true;
                canExecuteCalls.ShouldBe(1);
                _stub.Command.CanExecute(0).ShouldBe(false);
                _stub.Command.CanExecute(2).ShouldBe(true);

                _stub.Int.ShouldBe(0);
                _stub.Command.Execute(2);
                _stub.Int.ShouldBe(2);
            }
        }

        [Theory]
        public void ShouldAssignCanExecuteCorrectlyWhenAttached(bool canExecute)
        {
            _binder.BindCommand(x => { }, x => x.Flag).To(x => x.Command);
            _stub.Flag = canExecute;

            using (_binder.Attach(_stub))
            {
                _stub.Command.CanExecute(null).ShouldBe(canExecute);
            }
        }

        [Test]
        public void ShouldOverrideCommands()
        {
            _binder.BindCommand(x => x.Int++, x => x.Flag).To(x => x.Command);
            _binder.BindCommand(x => x.Int += 10, x => x.Flag).To(x => x.Command);

            using (_binder.Attach(_stub))
            {
                _stub.Command.ShouldNotBeNull();
                _stub.Command.CanExecute(null).ShouldBe(false);

                _stub.Flag = true;
                _stub.Command.CanExecute(null).ShouldBe(true);

                _stub.Int.ShouldBe(0);
                _stub.Command.Execute(null);
                _stub.Int.ShouldBe(10);
            }
        }

        [Test]
        public void ShouldUnbindCommands()
        {
            _binder.BindCommand(x => x.Int++, x => x.Flag).To(x => x.Command);
            _binder.Unbind(x => x.Command);

            using (_binder.Attach(_stub))
            {
                _stub.Command.ShouldBe(null);
            }
        }

        [Test]
        public void ShouldUnbindCommandByKey()
        {
            _binder.BindCommand(x => x.Int++, x => x.Flag).OverrideKey("testCommand").To(x => x.Command);
            _binder.Unbind("testCommand");

            using (_binder.Attach(_stub))
            {
                _stub.Command.ShouldBe(null);
            }
        }

        [Test]
        public void ShouldNotCrashIfCanExecuteConditionChangesBeforeCommandIsAssigned()
        {
            _binder.Bind(x => x.Int >= 0).To(x => x.Flag);
            _binder.BindCommand(x => { }, x => x.Flag).To(x => x.Command);

            Should.NotThrow(() =>
            {
                using (_binder.Attach(_stub))
                {
                }
            });

            using (_binder.Attach(_stub))
            {
                _stub.Command.CanExecute(null).ShouldBe(true);
                _stub.Int = -1;
                _stub.Command.CanExecute(null).ShouldBe(false);
            }
        }

        [Test]
        public void ShouldAllowCustomCommandBindingDependency()
        {
            int canExecuteCalls = 0;
            _binder.BindCommand(x => { }, x=> ExternalCondition(x)).WithDependency(x => x.Flag).To(x => x.Command);

            using (_binder.Attach(_stub))
            {
                _stub.Command.CanExecuteChanged += (s, e) => { ++canExecuteCalls; };
                _stub.Command.CanExecute(null).ShouldBe(false);
                _stub.Flag = true;
                canExecuteCalls.ShouldBe(1);
                _stub.Command.CanExecute(null).ShouldBe(true);
            }
        }

        [Test]
        public void ShouldExecuteCommandInAppropriateMode()
        {
            bool executed = false;
            _binder.BindCommand(x => { executed = true; }, x => false).WithCanExecuteCheckMode(CommandCanExecuteCheckMode.DoNotCheck).To(x => x.Command);

            using (_binder.Attach(_stub))
            {
                _stub.Command.Execute(null);
                executed.ShouldBe(true);
            }
        }

        [Test]
        public void ShouldNotExecuteCommandInAppropriateMode()
        {
            bool executed = false;
            _binder.BindCommand(x => { executed = true; }, x => false).WithCanExecuteCheckMode(CommandCanExecuteCheckMode.DoNotExecute).To(x => x.Command);

            using (_binder.Attach(_stub))
            {
                _stub.Command.Execute(null);
                executed.ShouldBe(false);
            }
        }

        [Test]
        public void ShouldThrowOnExecuteCommandInAppropriateMode()
        {
            bool executed = false;
            _binder.BindCommand(x => { executed = true; }, x => false).WithCanExecuteCheckMode(CommandCanExecuteCheckMode.ThrowException).To(x => x.Command);

            using (_binder.Attach(_stub))
            {
                Action action = () => _stub.Command.Execute(null);
                action.ShouldThrow<InvalidOperationException>();
                executed.ShouldBe(false);
            }
        }

        private static bool ExternalCondition(UniversalStub stub)
        {
            return stub.Flag;
        }
    }
}