namespace RG.Tweening
{
    public enum EaseType
    {
        Linear,
        InSine, OutSine, InOutSine,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        InQuart, OutQuart, InOutQuart,
        InQuint, OutQuint, InOutQuint,
        InExpo, OutExpo, InOutExpo,
        InCirc, OutCirc, InOutCirc,
        InBack, OutBack, InOutBack,
        InElastic, OutElastic, InOutElastic,
        InBounce, OutBounce, InOutBounce
    }

    public enum RepeatType
    {
        Restart,
        Yoyo,
        Incremental
    }

    internal enum TweenType
    {
        Move, MoveX, MoveY, MoveZ,
        LocalMove, LocalMoveX, LocalMoveY, LocalMoveZ,
        Rotation, LocalRotation,
        Scale, ScaleX, ScaleY, ScaleZ,
        MaterialColor, MaterialAlpha,
        SpriteColor, SpriteAlpha,
        ImageColor, ImageAlpha,
        TextColor, TextAlpha,
        TMPColor, TMPAlpha,
        CanvasAlpha,
        UIMove, UIMoveX, UIMoveY,
        SizeDelta,
        CameraFOV, CameraOrthoSize,
        AudioVolume, AudioPitch,
        LightIntensity, LightColor,
        CustomValue, DelayedCall, Sequence,
        Jump
    }
}