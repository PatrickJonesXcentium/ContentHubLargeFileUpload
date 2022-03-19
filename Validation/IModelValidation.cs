using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ContentHubLargeFileUpload.Validation
{
    /// <summary>
    /// Interface with signle member that perfroms validation from object attributes.
    /// </summary>
    /// <typeparam name="T">Class type to validate</typeparam>
    public interface IModelValidation<T> where T : class
    {
        /// <summary>
        /// Member that performs validation
        /// </summary>
        /// <param name="modelInstance">Model instance to validate</param>
        /// <returns>(bool Valid, ICollection<ValidationResult> Errors)</returns>
        (bool Valid, ICollection<ValidationResult> Errors) ValidateModel(T modelInstance);
    }
}
