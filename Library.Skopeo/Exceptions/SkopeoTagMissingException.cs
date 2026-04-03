namespace Library.Skopeo.Exceptions;

public class SkopeoTagMissingException : Exception {
  public SkopeoTagMissingException(string message) : base(message) {
  }
}