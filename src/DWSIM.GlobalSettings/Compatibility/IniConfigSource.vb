Imports System.Globalization
Imports System.IO

Namespace Global.Nini.Ini

    Public Enum IniFileType
        WindowsStyle
    End Enum

    Public Class IniDocument

        Public Sub New(path As String, fileType As IniFileType)
            FilePath = path
        End Sub

        Public ReadOnly Property FilePath As String

    End Class

End Namespace

Namespace Global.Nini.Config

    Public Class IniConfigSource

        Private ReadOnly _configs As New Dictionary(Of String, IniConfig)(StringComparer.OrdinalIgnoreCase)

        Public Sub New(path As String)
            FilePath = path
            Load(path)
        End Sub

        Public Sub New(document As Nini.Ini.IniDocument)
            FilePath = document.FilePath
            Load(document.FilePath)
        End Sub

        Public ReadOnly Property FilePath As String

        Default Public ReadOnly Property Configs(name As String) As IniConfig
            Get
                Dim config As IniConfig = Nothing
                If _configs.TryGetValue(name, config) Then
                    Return config
                End If
                Return Nothing
            End Get
        End Property

        Public Function AddConfig(name As String) As IniConfig
            Dim config As IniConfig = Nothing
            If Not _configs.TryGetValue(name, config) Then
                config = New IniConfig(name)
                _configs.Add(name, config)
            End If
            Return config
        End Function

        Public Sub Save()
            Save(FilePath)
        End Sub

        Public Sub Save(path As String)
            Using writer As New StreamWriter(path, False)
                For Each section In _configs.Values
                    writer.Write("[")
                    writer.Write(section.Name)
                    writer.WriteLine("]")
                    For Each entry In section.Values
                        writer.Write(entry.Key)
                        writer.Write("=")
                        writer.WriteLine(entry.Value)
                    Next
                    writer.WriteLine()
                Next
            End Using
        End Sub

        Private Sub Load(path As String)
            If Not File.Exists(path) Then
                Return
            End If

            Dim current As IniConfig = Nothing

            For Each rawLine In File.ReadAllLines(path)
                Dim line = rawLine.Trim()
                If line = "" OrElse line.StartsWith(";") OrElse line.StartsWith("#") Then
                    Continue For
                End If

                If line.StartsWith("[") AndAlso line.EndsWith("]") Then
                    current = AddConfig(line.Substring(1, line.Length - 2).Trim())
                    Continue For
                End If

                Dim separator = line.IndexOf("="c)
                If separator < 0 Then
                    Continue For
                End If

                If current Is Nothing Then
                    current = AddConfig("")
                End If

                current.Set(line.Substring(0, separator).Trim(), line.Substring(separator + 1).Trim())
            Next
        End Sub

    End Class

    Public Class IniConfig

        Private ReadOnly _values As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        Public Sub New(name As String)
            Me.Name = name
        End Sub

        Public ReadOnly Property Name As String

        Public ReadOnly Property Values As IReadOnlyDictionary(Of String, String)
            Get
                Return _values
            End Get
        End Property

        Public Function GetValues() As String()
            Return _values.Values.ToArray()
        End Function

        Public Function [Get](key As String, Optional defaultValue As String = "") As String
            Return GetString(key, defaultValue)
        End Function

        Public Function GetString(key As String, Optional defaultValue As String = "") As String
            Dim value As String = Nothing
            If _values.TryGetValue(key, value) Then
                Return value
            End If
            Return defaultValue
        End Function

        Public Function GetBoolean(key As String, Optional defaultValue As Boolean = False) As Boolean
            Dim value = GetString(key, Nothing)
            Dim parsed As Boolean
            If value IsNot Nothing AndAlso Boolean.TryParse(value, parsed) Then
                Return parsed
            End If
            Return defaultValue
        End Function

        Public Function GetInt(key As String, Optional defaultValue As Integer = 0) As Integer
            Dim value = GetString(key, Nothing)
            Dim parsed As Integer
            If value IsNot Nothing AndAlso Integer.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) Then
                Return parsed
            End If
            Return defaultValue
        End Function

        Public Function GetFloat(key As String, Optional defaultValue As Single = 0.0F) As Single
            Dim value = GetString(key, Nothing)
            Dim parsed As Single
            If value IsNot Nothing AndAlso Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                Return parsed
            End If
            Return defaultValue
        End Function

        Public Function GetDouble(key As String, Optional defaultValue As Double = 0.0R) As Double
            Dim value = GetString(key, Nothing)
            Dim parsed As Double
            If value IsNot Nothing AndAlso Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                Return parsed
            End If
            Return defaultValue
        End Function

        Public Sub [Set](key As Object, value As Object)
            _values(Convert.ToString(key, CultureInfo.InvariantCulture)) = Convert.ToString(value, CultureInfo.InvariantCulture)
        End Sub

    End Class

End Namespace
