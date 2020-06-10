using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Importers
{
    public class NameGenerator
    {
        public static string GetRandomName()
        {
            return Names[mRandom.Next(Names.Length)];
        }

        public static string GetRandomSurname()
        {
            return Surnames[mRandom.Next(Surnames.Length)];
        }

        public static string GetTeamName(long id)
        {
            var idx = id % TeamNames.Length;
            return TeamNames[idx];
        }

        public static string GetRandomEmail(string name)
        {
            name = name.ToLower()
                .Replace('á', 'a')
                .Replace('é', 'e')
                .Replace('í', 'i')
                .Replace('ó', 'o')
                .Replace('ú', 'u')
                .Replace(' ', '.');

            var rnd = mRandom.Next(1000);
            return $"{name}{rnd}@{Domains[0]}";
        }

        private static Random mRandom = new Random((int)DateTime.Now.Ticks);
        private static readonly string[] Names = new string[] { "Hugo", "Lucas", "Martín", "Mateo", "Daniel", "Alejandro", "Pablo", "Eric", "Leo", "Enzo", "Adrián", "Álvaro", "Manuel", "Mario", "David", "Izan", "Álex", "Diego", "Dylan", "Oliver", "Marco", "Thiago", "Marcos", "Marc", "Sergio", "Javier", "Luca", "Gonzalo", "Carlos", "Nicolás", "Iván", "Antonio", "Ángel", "Bruno", "Miguel", "Héctor", "Gabriel", "Jorge", "Iker", "Gael", "Juan", "Rodrigo", "Jesús", "Samuel", "José", "Aarón", "Ian", "Rubén", "Julen", "Aitor", "Darío", "Liam", "Alan", "Pau", "Joel", "Alberto", "Pol", "Jaime", "Nil", "Francisco", "Luis", "Pedro", "Asier", "Saúl", "Aleix", "Unai", "Biel", "Guillermo", "Santiago", "Víctor", "Alonso", "Neizan", "Rafael", "Víctor", "Noah", "Áxel", "Isaac", "Jan", "Martí", "Raúl", "Aimar", "Cristian", "Andrés", "Gerard", "Jordi", "Roberto", "Adam", "Derek", "Eloy", "Teo", "Abraham", "Ismael", "Kilian", "Noel", "Joan", "Romeo", "Yeray", "Miguel Ángel", "Ander", "Eduardo" };
        private static readonly string[] Surnames = new string[] { "González", "Rodríguez", "Gómez", "Huertas", "López", "Díaz", "Martínez", "Pérez", "García", "Sánchez", "Romero", "Sosa", "Álvarez", "Torres", "Ruiz", "Ramírez", "Flores", "Acosta", "Benítez", "Medina", "Suárez", "Herrera", "Aguirre", "Pereyra", "Gutiérrez", "Molina", "Silva", "Castro", "Rojas", "Ortiz", "Luna", "Juárez", "Cabrera", "Ríos", "Ferreyra", "Godoy", "Morales", "Domínguez", "Moreno", "Peralta", "Vega", "Carrizo", "Quiroga", "Castillo", "Ledesma", "Ojeda", "Ponce", "Vera", "Vázquez", "Villalba", "Cardozo", "Navarro", "Ramos", "Arias", "Coronel", "Córdoba", "Figueroa", "Correa", "Cáceres", "Vargas", "Maldonado", "Mansilla", "Farías", "Rivero", "Paz", "Miranda", "Roldán", "Méndez", "Lucero", "Cruz", "Hernández", "Páez", "Blanco", "Mendoza", "Barrios", "Escobar", "Ávila", "Soria", "Leiva", "Martín", "Maidana", "Moyano", "Campos", "Olivera", "Duarte", "Soto", "Bravo", "Valdez", "Toledo", "Velázquez", "Montenegro", "Leguizamón", "Chávez", "Arce" };
        private static readonly string[] Domains = new string[] { "example.com" };
        private static readonly string[] TeamNames = new string[] { "Justo Vilar", "Domine Port", "Nador Milagrosa", "Arquitecto Alfaro", "Barrio de la Luz", "Blocs Platja A", "Lepanto Don Juan A", "Matías Perelló", "Bilbao-Maximiliano Thous", "Card. Benlloch", "Galacticos", "Sikieres", "Sporting de King Kong", "Perla Ventera", "Tres Camins", "Pinedo", "Ind. Santos", "Azcarraga", "Zapadores", "In. Merced" };
    }
}
