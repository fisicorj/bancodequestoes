using System.Runtime.Versioning;

// Mesma declaração de Program.cs (projeto principal): sem isso, o projeto de
// testes "não sabe" que só roda em windows/linux/macos, e o analisador
// (CA1416) avisa em toda chamada pra dentro do projeto principal como se
// pudesse rodar em browser/iOS também — centenas de avisos de ruído, nenhum
// bug real.
[assembly: SupportedOSPlatform("windows")]
[assembly: SupportedOSPlatform("linux")]
[assembly: SupportedOSPlatform("macos")]
