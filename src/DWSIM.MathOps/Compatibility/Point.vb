Namespace Global.DWSIM.DrawingTools.Point

    Public Class Point

        Public Property X As Double

        Public Property Y As Double

        Public Sub New()
        End Sub

        Public Sub New(x As Single, y As Single)
            Me.X = x
            Me.Y = y
        End Sub

        Public Overrides Function ToString() As String
            Return "{X = " + X.ToString() + ", Y = " + Y.ToString() + "}"
        End Function

    End Class

End Namespace
