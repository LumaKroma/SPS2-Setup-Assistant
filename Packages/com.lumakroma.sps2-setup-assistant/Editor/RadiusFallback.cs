using System;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    internal static class RadiusFallback
    {
        internal struct Result { internal double Zero,Full; internal string Reason; }
        private static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        internal static bool Valid(double zero,double full)=>Finite(zero)&&Finite(full)&&zero>=0&&full>0&&zero<=full;
        internal static float Native1459Full(float scale)
        {
            if(!Finite(scale)||scale<=0)throw new InvalidOperationException("Socket scale が不明または無効です。");
            return .25f/Math.Max(Math.Abs(scale),.000001f);
        }
        internal static Result Select(bool existingSocket,bool hasSaved,bool savedEnabled,double savedZero,double savedFull,double manualZero,double manualFull)
        {
            if(existingSocket) {
                if(!hasSaved)throw new InvalidOperationException("既存 Socket の半径設定を対応付けできません。保全のため停止します。");
                if(savedEnabled&&Valid(savedZero,savedFull))return new Result{Zero=savedZero,Full=savedFull,Reason="既存の保存済み半径を保持します。native 編集の保護検査は維持します。"};
                if(!savedEnabled||savedZero==0&&savedFull==0)return new Result{Reason="既存の重み未適用状態を保持し、他の生成を続けます。"};
                throw new InvalidOperationException("既存の保存済み半径が不正です。native 設定を推測で変更せず停止します。");
            }
            return new Result{Reason="Fury 標準値を安全に算出できないため重み補正は未適用で、他の生成を続けます。手動入力値は bank に保持します。"};
        }
    }
}
