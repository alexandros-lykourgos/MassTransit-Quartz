﻿namespace MassTransit.QuartzIntegration.Tests
{
    using System.Threading;
    using Magnum.Extensions;
    using NUnit.Framework;
    using Quartz;
    using Quartz.Impl;
    using Scheduling;


    [TestFixture]
    public class Specifying_an_event_in_the_past
    {
        [Test]
        public void Should_properly_send_the_message()
        {
            _bus.ScheduleMessage((-1).Hours().FromUtcNow(), new A { Name = "Joe" }, x =>
            {
                x.SetHeader("TestHeader", "Test");
            });

            Assert.IsTrue(_receivedA.WaitOne(Utils.Timeout), "Message A not handled");

            Assert.IsTrue(_received.Headers["TestHeader"].Equals("Test"));
        }


        class A 
        {
            public string Name { get; set; }
        }

        IScheduler _scheduler;
        IServiceBus _bus;
        ManualResetEvent _receivedA;
        IConsumeContext<A> _received;

        [TestFixtureSetUp]
        public void Setup_quartz_service()
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            _scheduler = schedulerFactory.GetScheduler();

            _receivedA = new ManualResetEvent(false);

            _bus = ServiceBusFactory.New(x =>
            {
                x.ReceiveFrom("loopback://localhost/quartz");
                x.UseJsonSerializer();

                x.Subscribe(s =>
                {
                    s.Handler<A>((msg, context) =>
                    {
                        _received = msg;
                        _receivedA.Set();
                    });
                    s.Consumer(() => new ScheduleMessageConsumer(_scheduler));
                });
            });

            _scheduler.JobFactory = new MassTransitJobFactory(_bus);
            _scheduler.Start();
        }

        [TestFixtureTearDown]
        public void Teardown_quartz_service()
        {
            if (_scheduler != null)
                _scheduler.Standby();
            if (_bus != null)
                _bus.Dispose();
            if (_scheduler != null)
                _scheduler.Shutdown();
        }
    }
}
