namespace VentaFacil.Core
{
    /// <summary>
    /// Secreto de DESARROLLO: solo para que el proyecto compile en cualquier computadora.
    /// El secreto real está fuera del repositorio, en ~/.ventafacil-licencias/LicenciaSecreto.cs;
    /// cuando existe, se usa ese archivo y este se ignora (ver VentaFacil.csproj).
    /// Un programa compilado con este secreto NO acepta las claves reales.
    /// </summary>
    internal static class LicenciaSecreto
    {
        public const string Valor = "VentaFacil-secreto-de-desarrollo-no-es-el-real";
        public const bool Desarrollo = true;
    }
}
