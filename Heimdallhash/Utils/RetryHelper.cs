using System;
using System.Threading.Tasks;

namespace Heimdallhash.Utils
{
    public static class RetryHelper
    {
        public static async Task<T> EjecutarConReintentosAsync<T>(
            Func<Task<T>> accion,
            int maxIntentos,
            int delayMilisegundos)
        {
            int intentos = 0;

            while (true)
            {
                try
                {
                    return await accion();
                }
                catch (IOException) when (intentos < maxIntentos)
                {
                    intentos++;
                    await Task.Delay(delayMilisegundos);
                }
                catch (UnauthorizedAccessException) when (intentos < maxIntentos)
                {
                    intentos++;
                    await Task.Delay(delayMilisegundos);
                }
                catch
                {
                    throw; // otros errores no se reintentan
                }
            }
        }
    }
}
