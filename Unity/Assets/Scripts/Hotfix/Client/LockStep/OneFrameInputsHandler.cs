using System;

namespace ET.Client
{
    [MessageHandler(SceneType.LockStep)]
    public class OneFrameInputsHandler: AMHandler<OneFrameInputs>
    {
        protected override async ETTask Run(Session session, OneFrameInputs input)
        {
            Room room = session.DomainScene().GetComponent<Room>();
            
            Log.Debug($"OneFrameInputs: {room.AuthorityFrame + 1} {input.ToJson()}");
            
            FrameBuffer frameBuffer = room.FrameBuffer;

            ++room.AuthorityFrame;
            // 服务端返回的消息比预测的还早
            if (room.AuthorityFrame > room.PredictionFrame)
            {
                OneFrameInputs authorityFrame = frameBuffer.FrameInputs(room.AuthorityFrame);
                input.CopyTo(authorityFrame);
            }
            else
            {

                // 服务端返回来的消息，跟预测消息对比
                OneFrameInputs predictionInput = frameBuffer.FrameInputs(room.AuthorityFrame);
                // 对比失败有两种可能，
                // 1是别人的输入预测失败，这种很正常，
                // 2 自己的输入对比失败，这种情况是自己发送的消息比服务器晚到了，服务器使用了你的上一次输入
                // 回滚重新预测的时候，自己的输入不用变化
                if (input != predictionInput)
                {
                    input.CopyTo(predictionInput);
                    // 回滚到frameBuffer.AuthorityFrame
                    LSHelper.Rollback(room, room.AuthorityFrame);
                }
                else
                {
                    LSClientUpdater clientUpdater = room.GetComponent<LSClientUpdater>();
                    clientUpdater.Record(room.AuthorityFrame);
                }
            }

            // 回收消息，减少GC
            NetServices.Instance.RecycleMessage(input);
            await ETTask.CompletedTask;
        }
    }
}