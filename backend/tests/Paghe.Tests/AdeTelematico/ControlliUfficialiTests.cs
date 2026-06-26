using Paghe.Core.AdeTelematico;

namespace Paghe.Tests.AdeTelematico;

public class ControlliUfficialiTests
{
    [Fact]
    public void Costruisce_argomenti_e_percorsi_output()
    {
        var richiesta = new RichiestaControlloUfficiale
        {
            FileInput = Path.Combine("export", "da-controllare", "F24_001.txt"),
            Classpath = "SuppTelem.jar:ControlliF24A0.jar",
            ClasseMain = "it.finanze.entrate.dic.main.MF24A0",
            CartellaOutput = "out",
        };

        var arg = ControlliUfficialiRunnerJava.CostruisciArgomenti(richiesta);

        Assert.Equal(
            ["-cp", "SuppTelem.jar:ControlliF24A0.jar", "it.finanze.entrate.dic.main.MF24A0",
             richiesta.FileInput, arg.FileDiagnostico, arg.FileControllato],
            arg.Argomenti);
        Assert.Equal(Path.Combine("out", "F24_001.dgn"), arg.FileDiagnostico);
        Assert.Equal(Path.Combine("out", "F24_001.dcm"), arg.FileControllato);
    }

    [Fact]
    public void Classpath_mancante_lancia()
    {
        var richiesta = new RichiestaControlloUfficiale
        {
            FileInput = "F24.txt",
            Classpath = "",
            ClasseMain = "it.finanze.entrate.dic.main.MF24A0",
        };

        Assert.Throws<ArgumentException>(() => ControlliUfficialiRunnerJava.CostruisciArgomenti(richiesta));
    }
}
